using System;
using UnityEngine;

/// <summary>
/// Run map: generation modes (multi-path or unique spanning tree), player steps, move limit, corruption,
/// and map-event routing when <see cref="RunManager.UseMapBasedRun"/> is true.
/// </summary>
public sealed class MapMovementManager : MonoBehaviour
{
    [Tooltip("Used when not in a map-based run, or when the current MapActDefinitionSO is missing layout fields.")]
    [SerializeField] private int gridWidth = 4;
    [Tooltip("Used when not in a map-based run, or when the current MapActDefinitionSO is missing layout fields.")]
    [SerializeField] private int gridHeight = 4;
    [Tooltip("Used when not in a map-based run, or when the current MapActDefinitionSO is missing layout fields.")]
    [SerializeField] private int moveLimit = 8;
    [SerializeField] private bool useFixedSeed;
    [SerializeField] private int fixedSeed;
    [Header("Connectivity")]
    [Tooltip("First map when the scene loads (or after RegenerateMap() from code).")]
    [SerializeField] private MapConnectivityMode initialMapMode = MapConnectivityMode.MultiPathRandom;
    [Tooltip("Acts 2–3 (index ≥ 1) use this connectivity when a new map is generated.")]
    [SerializeField] private MapConnectivityMode mapModeAfterBossReach = MapConnectivityMode.UniquePathSpanningTree;
    [SerializeField] private UIMapGridView mapView;
    [SerializeField] private UIMapMoveCounterUI moveCounterUI;
    [Header("Move limit overflow (corruption)")]
    [Tooltip("Damage on the first move after exceeding move limit (move limit + 1).")]
    [SerializeField, Min(0)] private int overflowDamageBase = 3;
    [Tooltip("Added to damage for each further over-limit move (2nd over move = base + this, 3rd = base + 2×this, …).")]
    [SerializeField, Min(0)] private int overflowDamageIncreasePerMove = 2;
    [Tooltip("World position for floating damage text (same pipeline as combat). If unset, uses a point in front of the main camera.")]
    [SerializeField] private Transform overflowDamageNumberWorldAnchor;
    [Header("Presentation (map run)")]
    [SerializeField] private MapPresentationSO mapPresentation;
    [SerializeField] private MapShrineChoicePanel shrineChoicePanel;

    private MapGrid _grid;
    private System.Random _rng;
    private int _effectiveGridWidth;
    private int _effectiveGridHeight;
    private int _effectiveMoveLimit;

    public int MovesTaken { get; private set; }
    public int MoveLimit => _effectiveMoveLimit;
    public MapGrid Grid => _grid;
    public Vector2Int PlayerGridPosition { get; private set; }

    public Vector2Int StartPosition => new Vector2Int(0, 0);

    public Vector2Int BossPosition =>
        _grid != null ? new Vector2Int(_grid.Width - 1, _grid.Height - 1) : Vector2Int.zero;

    /// <summary>Fired after a move when <see cref="MovesTaken"/> is greater than <see cref="MoveLimit"/>.</summary>
    public event Action OnCorruptionTriggered;

    public event Action OnPlayerMoved;
    public event Action OnBossReached;

    private void Awake()
    {
        _rng = useFixedSeed ? new System.Random(fixedSeed) : new System.Random();
        RefreshEffectiveMapLayoutFromActOrDefaults();
    }

    private void Start()
    {
        RefreshEffectiveMapLayoutFromActOrDefaults();

        if (RunManager.Instance != null && RunManager.Instance.UseMapBasedRun &&
            RunManager.Instance.TryRestorePersistedMap(out var restoredGrid, out var playerCell, out var moves))
        {
            _grid = restoredGrid;
            PlayerGridPosition = playerCell;
            MovesTaken = moves;
            mapView?.Present(_grid, this, mapPresentation);
            moveCounterUI?.Bind(this);
            return;
        }

        RegenerateMap();
    }

    /// <summary>Rebuilds grid and resets player at start. Act 1+ may use <see cref="mapModeAfterBossReach"/>.</summary>
    public void RegenerateMap()
    {
        RegenerateMapInternal();
    }

    private void RegenerateMapInternal()
    {
        RefreshEffectiveMapLayoutFromActOrDefaults();

        if (_effectiveGridWidth < 1 || _effectiveGridHeight < 1)
        {
            Debug.LogError("MapMovementManager: effective grid width and height must be at least 1.", this);
            return;
        }

        var usePostFirstActMode = RunManager.Instance != null && RunManager.Instance.UseMapBasedRun &&
                                  RunManager.Instance.CurrentActIndex > 0;
        var mode = usePostFirstActMode ? mapModeAfterBossReach : initialMapMode;
        var evtParams = RunManager.Instance != null && RunManager.Instance.UseMapBasedRun
            ? RunManager.Instance.GetMapGenerationParamsForCurrentAct()
            : MapGenerationEventsParams.Default;

        _grid = MapGridGenerator.Generate(_effectiveGridWidth, _effectiveGridHeight, _rng, mode, MapGridGenerator.DefaultMaxRegenerateAttempts, evtParams);
        RunManager.Instance?.OnNewMapGeneratedCleanupDraws();
        MovesTaken = 0;
        PlayerGridPosition = StartPosition;

        mapView?.Present(_grid, this, mapPresentation);
        moveCounterUI?.Bind(this);
    }

    /// <summary>Move to an orthogonally adjacent tile if the current tile has a directed exit toward it.</summary>
    public bool TryMoveTo(Vector2Int target)
    {
        if (_grid == null || !_grid.Contains(target))
            return false;

        var from = PlayerGridPosition;
        if (from == target)
            return false;

        if (!IsOrthogonalAdjacent(from, target))
            return false;

        var dir = DirectionFromTo(from, target);
        if (!_grid.HasExit(from.x, from.y, dir))
            return false;

        PlayerGridPosition = target;
        MovesTaken++;
        OnPlayerMoved?.Invoke();

        if (MovesTaken > _effectiveMoveLimit)
        {
            OnCorruptionTriggered?.Invoke();
            if (RunManager.Instance != null && RunManager.Instance.UseMapBasedRun)
            {
                var overCount = MovesTaken - _effectiveMoveLimit;
                var damage = overflowDamageBase + (overCount - 1) * overflowDamageIncreasePerMove;
                if (damage > 0)
                {
                    var anchor = ResolveOverflowDamageWorldAnchor();
                    RunManager.Instance.ApplyRunMapDamage(damage, anchor);
                }
            }

            if (!MapCorruptionUtility.TryCloseOneRandomExitPreservingPath(_grid, PlayerGridPosition, StartPosition, BossPosition, _rng))
                Debug.LogWarning("MapMovementManager: corruption could not close any passage without leaving a path from the player to the boss — map unchanged.", this);
            mapView?.RefreshAllTileExits();
        }

        if (PlayerGridPosition == BossPosition)
            OnBossReached?.Invoke();

        ResolveTileAfterMoveIfMapRun();

        mapView?.RefreshPlayerIcon();
        moveCounterUI?.Refresh();
        return true;
    }

    private void MarkCurrentTileConsumedAndRefresh()
    {
        var c = PlayerGridPosition;
        var t = _grid.Get(c.x, c.y);
        t.eventConsumed = true;
        _grid.SetTile(c.x, c.y, t);
        mapView?.RefreshTile(_grid, c);
    }

    private void ResolveTileAfterMoveIfMapRun()
    {
        if (RunManager.Instance == null || !RunManager.Instance.UseMapBasedRun || _grid == null)
            return;

        var tile = _grid.Get(PlayerGridPosition);
        if (tile.eventConsumed)
            return;

        switch (tile.eventType)
        {
            case MapEventType.None:
                MarkCurrentTileConsumedAndRefresh();
                return;
            case MapEventType.CombatNormal:
                MarkCurrentTileConsumedAndRefresh();
                RunManager.Instance.PersistAndLoadFightScene(_grid.Clone(), PlayerGridPosition, MovesTaken, EnemyRank.Normal, false);
                return;
            case MapEventType.CombatElite:
                MarkCurrentTileConsumedAndRefresh();
                RunManager.Instance.PersistAndLoadFightScene(_grid.Clone(), PlayerGridPosition, MovesTaken, EnemyRank.Elite, false);
                return;
            case MapEventType.CombatBoss:
                MarkCurrentTileConsumedAndRefresh();
                RunManager.Instance.PersistAndLoadFightScene(_grid.Clone(), PlayerGridPosition, MovesTaken, EnemyRank.Boss, true);
                return;
            case MapEventType.Shop:
                MarkCurrentTileConsumedAndRefresh();
                RunManager.Instance.PersistAndLoadShopScene(_grid.Clone(), PlayerGridPosition, MovesTaken);
                return;
            case MapEventType.Shrine:
                if (shrineChoicePanel == null)
                {
                    Debug.LogError("MapMovementManager: assign shrineChoicePanel for Shrine tiles.", this);
                    return;
                }

                if (!shrineChoicePanel.TryOpenPanel())
                    return;

                MarkCurrentTileConsumedAndRefresh();
                return;
            case MapEventType.Unknown:
            {
                MarkCurrentTileConsumedAndRefresh();
                var unknown = RunManager.Instance.DrawUnknownMapEvent();
                if (unknown != null)
                    Debug.Log($"Unknown tile: {unknown.DisplayLabel}" + (string.IsNullOrEmpty(unknown.description) ? "" : $" — {unknown.description}"));
                else
                    Debug.Log("Unknown tile: no possibleUnknownEvents configured for this act.");
                return;
            }
            default:
                return;
        }
    }

    public bool IsBoss(Vector2Int cell) => cell == BossPosition;

    public bool IsStart(Vector2Int cell) => cell == StartPosition;

    private static bool IsOrthogonalAdjacent(Vector2Int a, Vector2Int b)
    {
        var dx = Mathf.Abs(a.x - b.x);
        var dy = Mathf.Abs(a.y - b.y);
        return dx + dy == 1;
    }

    private static MapCardinalDirection DirectionFromTo(Vector2Int from, Vector2Int to)
    {
        var d = to - from;
        if (d.x == 1) return MapCardinalDirection.Right;
        if (d.x == -1) return MapCardinalDirection.Left;
        if (d.y == 1) return MapCardinalDirection.Bottom;
        if (d.y == -1) return MapCardinalDirection.Top;
        throw new InvalidOperationException("DirectionFromTo: cells are not orthogonally adjacent.");
    }

    private Vector3 ResolveOverflowDamageWorldAnchor()
    {
        if (overflowDamageNumberWorldAnchor != null)
            return overflowDamageNumberWorldAnchor.position;
        var cam = Camera.main;
        if (cam != null)
            return cam.transform.position + cam.transform.forward * 8f;
        return Vector3.zero;
    }

    /// <summary>
    /// Map runs: <see cref="MapActDefinitionSO.gridWidth"/>, <see cref="MapActDefinitionSO.gridHeight"/>, <see cref="MapActDefinitionSO.moveLimit"/>.
    /// Otherwise uses serialized defaults on this component.
    /// </summary>
    private void RefreshEffectiveMapLayoutFromActOrDefaults()
    {
        if (RunManager.Instance != null && RunManager.Instance.UseMapBasedRun)
        {
            var act = RunManager.Instance.GetCurrentMapActDefinitionOrNull();
                       if (act != null)
            {
                _effectiveGridWidth = Mathf.Max(1, act.gridWidth);
                _effectiveGridHeight = Mathf.Max(1, act.gridHeight);
                _effectiveMoveLimit = Mathf.Max(1, act.moveLimit);
                return;
            }

            Debug.LogError("MapMovementManager: map-based run has no MapActDefinitionSO for current act — using inspector grid/move limit fallbacks.", this);
        }

        _effectiveGridWidth = Mathf.Max(1, gridWidth);
        _effectiveGridHeight = Mathf.Max(1, gridHeight);
        _effectiveMoveLimit = Mathf.Max(1, moveLimit);
    }
}
