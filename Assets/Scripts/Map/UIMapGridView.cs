using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Spawns a tile UI per cell and drives a separate player marker (sprite from active <see cref="PlayerDataSO.MapPlayerIcon"/>, else <see cref="MapPresentationSO"/>).</summary>
public class UIMapGridView : MonoBehaviour
{
    static readonly AnimationCurve LinearMoveProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField] private RectTransform tilesParent;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private UIMapTileView tilePrefab;
    [Header("Player marker")]
    [Tooltip("RectTransform for the pawn; anchored position is driven to each tile’s center (sibling of the grid is typical).")]
    [SerializeField] private RectTransform playerMarker;
    [Tooltip("Optional. If unset, uses Image on playerMarker.")]
    [SerializeField] private Image playerMarkerImage;
    [Header("Move start visuals")]
    [Tooltip("When the pawn starts moving, visited/standing background scale (and hover scale) lerp toward their post-move state over this many seconds (linear). Background color lerps use each tile's Background Color Transition Seconds and, on multi-step moves, start when the pawn leaves the tile before the destination.")]
    [SerializeField, Min(0f)] private float standingVisitedBackgroundTransitionSeconds = 0.35f;

    /// <summary>While a multi-step move animates, background color lerps wait until the pawn leaves the tile before the destination.</summary>
    private bool _deferBackgroundColorLerpUntilFinalSegment;
    /// <summary>While a multi-step move animates, the click target keeps its pre-move tint until the final segment.</summary>
    private bool _deferDestinationSelectedVisualUntilFinalSegment;

    private UIMapTileView[,] _tiles;
    private MapMovementManager _manager;
    private MapPresentationSO _presentation;
    private Coroutine _markerMoveRoutine;
    private Coroutine _markerLayoutSnapRoutine;
    private RectTransform _markerParentRt;
    private Canvas _canvas;
    private readonly HashSet<Vector2Int> _reachableMoveTargetsScratch = new HashSet<Vector2Int>();
    /// <summary>While the pawn animates, the Selected visual uses this cell (the click target), not <see cref="MapMovementManager.PlayerGridPosition"/>.</summary>
    private Vector2Int? _tileStateSelectedCellOverride;
    /// <summary>While the pawn animates, “standing here” background/arrows use this cell until the move completes.</summary>
    private Vector2Int? _standingVisualCellOverride;

    private void Awake()
    {
        if (tilesParent == null)
            Debug.LogError("UIMapGridView: assign tilesParent.", this);
        if (tilePrefab == null)
            Debug.LogError("UIMapGridView: assign tilePrefab.", this);
        if (gridLayout == null && tilesParent != null)
            gridLayout = tilesParent.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
            Debug.LogError("UIMapGridView: assign gridLayout (or add GridLayoutGroup to tilesParent).", this);

        if (playerMarkerImage == null && playerMarker != null)
            playerMarkerImage = playerMarker.GetComponent<Image>();

        if (playerMarker != null)
        {
            _markerParentRt = playerMarker.parent as RectTransform;
            _canvas = playerMarker.GetComponentInParent<Canvas>();
        }
    }

    public void Present(MapGrid grid, MapMovementManager manager, MapPresentationSO presentation = null)
    {
        if (grid == null || manager == null || tilesParent == null || tilePrefab == null || gridLayout == null)
            return;

        if (_markerMoveRoutine != null)
        {
            StopCoroutine(_markerMoveRoutine);
            _markerMoveRoutine = null;
        }

        if (_markerLayoutSnapRoutine != null)
        {
            StopCoroutine(_markerLayoutSnapRoutine);
            _markerLayoutSnapRoutine = null;
        }

        ClearMoveVisualOverrides();

        DetachPlayerMarkerBeforeClearingTiles();

        _manager = manager;
        _presentation = presentation;
        ClearChildren();

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = grid.Width;

        _tiles = new UIMapTileView[grid.Width, grid.Height];

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                var tile = grid.Get(x, y);
                var view = Instantiate(tilePrefab, tilesParent);
                _tiles[x, y] = view;
                var isStart = manager.IsStart(cell);
                var isBoss = manager.IsBoss(cell);
                view.Setup(cell, tile, isStart, isBoss, manager, presentation);
            }
        }

        ApplyPlayerMarkerSpriteFromPresentation();
        RefreshAllTileExits();

        if (tilesParent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(tilesParent);
        Canvas.ForceUpdateCanvases();

        SnapPlayerMarkerToCell(manager.PlayerGridPosition);
        RefreshPlayerStandingVisuals();

        if (_markerLayoutSnapRoutine != null)
            StopCoroutine(_markerLayoutSnapRoutine);
        if (!isActiveAndEnabled)
        {
            Debug.LogError(
                $"UIMapGridView on '{name}': cannot present while inactive — enable the Grid hierarchy before calling Present.",
                this);
            return;
        }

        _markerLayoutSnapRoutine = StartCoroutine(CoSnapPlayerMarkerAfterLayout());
    }

    /// <summary>Re-applies tile visuals after <see cref="MapTile.eventConsumed"/> changes.</summary>
    public void RefreshTile(MapGrid grid, Vector2Int cell)
    {
        if (grid == null || _tiles == null || _manager == null || !grid.Contains(cell))
            return;
        if (cell.x < 0 || cell.x >= _tiles.GetLength(0) || cell.y < 0 || cell.y >= _tiles.GetLength(1))
            return;
        var view = _tiles[cell.x, cell.y];
        if (view == null)
            return;
        var tile = grid.Get(cell.x, cell.y);
        var isStart = _manager.IsStart(cell);
        var isBoss = _manager.IsBoss(cell);
        view.Setup(cell, tile, isStart, isBoss, _manager, _presentation);
    }

    public void RefreshAllTileExits()
    {
        var grid = _manager != null ? _manager.Grid : null;
        if (grid == null || _tiles == null)
            return;

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var v = _tiles[x, y];
                if (v != null)
                    v.RefreshExits(grid);
            }
        }

        RefreshPlayerStandingVisuals();
    }

    /// <summary>Moves the marker from <paramref name="fromCell"/> to <paramref name="toCell"/>, then invokes <paramref name="onComplete"/>.</summary>
    public void MovePlayerMarkerThen(Vector2Int fromCell, Vector2Int toCell, float finalSegmentDurationSeconds, AnimationCurve moveCurve, Action onComplete)
    {
        MovePlayerMarkerAlongPath(
            new[] { fromCell, toCell },
            finalSegmentDurationSeconds,
            intermediateSegmentDurationSeconds: 0f,
            moveCurve,
            null,
            onComplete);
    }

    /// <summary>
    /// While the pawn animates along <paramref name="path"/>, updates the standing-tile visual to the cell the marker has reached.
    /// </summary>
    public void SetMoveAnimationStandingCell(Vector2Int cell) => _standingVisualCellOverride = cell;

    /// <summary>Moves the marker through each cell in <paramref name="path"/> (in order), then invokes <paramref name="onComplete"/>.</summary>
    public void MovePlayerMarkerAlongPath(
        IReadOnlyList<Vector2Int> path,
        float finalSegmentDurationSeconds,
        float intermediateSegmentDurationSeconds,
        AnimationCurve moveCurve,
        Action<int> onReachedPathIndex,
        Action onComplete)
    {
        if (_tiles == null || _manager == null || path == null || path.Count < 2)
        {
            onComplete?.Invoke();
            return;
        }

        var fromCell = path[0];
        var toCell = path[path.Count - 1];
        var segmentCount = path.Count - 1;
        var finalDuration = Mathf.Max(0f, finalSegmentDurationSeconds);
        var intermediateDuration = Mathf.Max(0f, intermediateSegmentDurationSeconds);
        var totalDuration = ComputePathMoveDuration(segmentCount, finalDuration, intermediateDuration);
        var animateMove = totalDuration > 0f && fromCell != toCell;
        var multiStepAnimatedMove = animateMove && path.Count > 2;
        _deferBackgroundColorLerpUntilFinalSegment = multiStepAnimatedMove;
        _deferDestinationSelectedVisualUntilFinalSegment = multiStepAnimatedMove;
        _tileStateSelectedCellOverride = animateMove && !multiStepAnimatedMove ? toCell : (Vector2Int?)null;
        _standingVisualCellOverride = animateMove ? fromCell : (Vector2Int?)null;

        RefreshPlayerStandingVisuals();
        if (animateMove && standingVisitedBackgroundTransitionSeconds > 0f)
        {
            BeginStandingVisitedBackgroundTransitionsForMove(
                toCell,
                standingVisitedBackgroundTransitionSeconds,
                includeColorTransition: !multiStepAnimatedMove,
                includeScaleTransition: true);
        }

        if (playerMarker == null)
        {
            ClearMoveVisualOverrides();
            onComplete?.Invoke();
            return;
        }

        if (_markerMoveRoutine != null)
        {
            StopCoroutine(_markerMoveRoutine);
            _markerMoveRoutine = null;
        }

        if (_markerLayoutSnapRoutine != null)
        {
            StopCoroutine(_markerLayoutSnapRoutine);
            _markerLayoutSnapRoutine = null;
        }

        var curve = moveCurve != null && moveCurve.keys.Length > 0
            ? moveCurve
            : AnimationCurve.Linear(0f, 0f, 1f, 1f);

        if (!animateMove)
        {
            _deferBackgroundColorLerpUntilFinalSegment = false;
            PlayLandingScaleDownAt(toCell);
            for (var i = 1; i < path.Count; i++)
                onReachedPathIndex?.Invoke(i);

            ClearMoveVisualOverrides();
            SnapPlayerMarkerToCell(toCell);
            RefreshPlayerStandingVisuals();
            onComplete?.Invoke();
            return;
        }

        _markerMoveRoutine = StartCoroutine(CoMovePlayerMarkerAlongPath(
            path,
            finalDuration,
            intermediateDuration,
            curve,
            onReachedPathIndex,
            onComplete));
    }

    static float ComputePathMoveDuration(int segmentCount, float finalDuration, float intermediateDuration)
    {
        if (segmentCount <= 0)
            return 0f;
        if (segmentCount == 1)
            return finalDuration;
        return intermediateDuration * (segmentCount - 1) + finalDuration;
    }

    private IEnumerator CoSnapPlayerMarkerAfterLayout()
    {
        yield return null;
        if (_manager == null || _tiles == null || playerMarker == null)
        {
            _markerLayoutSnapRoutine = null;
            yield break;
        }

        if (tilesParent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(tilesParent);
        Canvas.ForceUpdateCanvases();
        SnapPlayerMarkerToCell(_manager.PlayerGridPosition);
        _markerLayoutSnapRoutine = null;
    }

    private IEnumerator CoMovePlayerMarkerAlongPath(
        IReadOnlyList<Vector2Int> path,
        float finalSegmentDuration,
        float intermediateSegmentDuration,
        AnimationCurve curve,
        Action<int> onReachedPathIndex,
        Action onComplete)
    {
        var segmentCount = path.Count - 1;

        for (var segment = 0; segment < segmentCount; segment++)
        {
            var isFinalSegment = segment == segmentCount - 1;
            var segmentDuration = isFinalSegment ? finalSegmentDuration : intermediateSegmentDuration;

            if (isFinalSegment)
            {
                PlayLandingScaleDownAt(path[path.Count - 1]);
                if (_deferBackgroundColorLerpUntilFinalSegment || _deferDestinationSelectedVisualUntilFinalSegment)
                {
                    _deferBackgroundColorLerpUntilFinalSegment = false;
                    _deferDestinationSelectedVisualUntilFinalSegment = false;
                    _tileStateSelectedCellOverride = path[path.Count - 1];
                    BeginMoveEndBackgroundColorTransitionsForMove(path[path.Count - 1]);
                    RefreshPlayerStandingVisuals();
                }
            }

            yield return CoMovePlayerMarkerSegment(
                path[segment],
                path[segment + 1],
                segmentDuration,
                isFinalSegment ? curve : LinearMoveProgressCurve);
            onReachedPathIndex?.Invoke(segment + 1);
        }

        _markerMoveRoutine = null;
        ClearMoveVisualOverrides();
        onComplete?.Invoke();
    }

    private IEnumerator CoMovePlayerMarkerSegment(Vector2Int fromCell, Vector2Int toCell, float duration, AnimationCurve curve)
    {
        var w = _tiles.GetLength(0);
        var h = _tiles.GetLength(1);
        if (fromCell.x < 0 || fromCell.x >= w || fromCell.y < 0 || fromCell.y >= h ||
            toCell.x < 0 || toCell.x >= w || toCell.y < 0 || toCell.y >= h)
        {
            SnapPlayerMarkerToCell(toCell);
            yield break;
        }

        if (duration <= 0f)
        {
            playerMarker.anchoredPosition = GetCellCenterAnchoredInMarkerParent(toCell);
            yield break;
        }

        var start = GetCellCenterAnchoredInMarkerParent(fromCell);
        var end = GetCellCenterAnchoredInMarkerParent(toCell);
        playerMarker.anchoredPosition = start;

        var t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / duration);
            var k = curve.Evaluate(u);
            playerMarker.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
            yield return null;
        }

        playerMarker.anchoredPosition = end;
    }

    /// <summary>Runs the landing scale-down on the destination tile (call when the pawn leaves the tile before it).</summary>
    public void PlayLandingScaleDownAt(Vector2Int cell)
    {
        if (_tiles == null || _manager == null)
            return;
        var grid = _manager.Grid;
        if (grid == null || !grid.Contains(cell))
            return;
        if (cell.x < 0 || cell.x >= _tiles.GetLength(0) || cell.y < 0 || cell.y >= _tiles.GetLength(1))
            return;
        _tiles[cell.x, cell.y]?.PlayLandingScaleDown();
    }

    private void BeginStandingVisitedBackgroundTransitionsForMove(
        Vector2Int toCell,
        float durationSeconds,
        bool includeColorTransition = true,
        bool includeScaleTransition = true)
    {
        if (_tiles == null || _manager == null || _manager.Grid == null)
            return;

        var grid = _manager.Grid;
        var w = grid.Width;
        var h = grid.Height;
        if (_tiles.GetLength(0) != w || _tiles.GetLength(1) != h)
            return;

        _manager.CollectReachableMoveTargetsFrom(toCell, _reachableMoveTargetsScratch);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var v = _tiles[x, y];
                if (v == null)
                    continue;
                var cell = new Vector2Int(x, y);
                var standingEnd = toCell.x == x && toCell.y == y;
                MapTileUIViewState reachAtEnd;
                if (standingEnd)
                    reachAtEnd = MapTileUIViewState.Selected;
                else if (_reachableMoveTargetsScratch.Contains(cell))
                    reachAtEnd = MapTileUIViewState.Available;
                else
                    reachAtEnd = MapTileUIViewState.Idle;
                v.BeginStandingVisitedBackgroundTransition(
                    standingEnd,
                    reachAtEnd,
                    durationSeconds,
                    includeColorTransition,
                    includeScaleTransition);
            }
        }
    }

    /// <summary>Starts background color lerps toward post-move tints when the pawn leaves the tile before the destination.</summary>
    private void BeginMoveEndBackgroundColorTransitionsForMove(Vector2Int toCell)
    {
        if (_tiles == null || _manager == null || _manager.Grid == null)
            return;

        var grid = _manager.Grid;
        var w = grid.Width;
        var h = grid.Height;
        if (_tiles.GetLength(0) != w || _tiles.GetLength(1) != h)
            return;

        _manager.CollectReachableMoveTargetsFrom(toCell, _reachableMoveTargetsScratch);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var v = _tiles[x, y];
                if (v == null)
                    continue;
                var cell = new Vector2Int(x, y);
                var standingEnd = toCell.x == x && toCell.y == y;
                MapTileUIViewState reachAtEnd;
                if (standingEnd)
                    reachAtEnd = MapTileUIViewState.Selected;
                else if (_reachableMoveTargetsScratch.Contains(cell))
                    reachAtEnd = MapTileUIViewState.Available;
                else
                    reachAtEnd = MapTileUIViewState.Idle;
                v.BeginBackgroundColorTransitionForState(standingEnd, reachAtEnd);
            }
        }
    }

    public void SnapPlayerMarkerToCell(Vector2Int cell)
    {
        if (playerMarker == null || _tiles == null || _manager == null)
            return;
        if (cell.x < 0 || cell.x >= _tiles.GetLength(0) || cell.y < 0 || cell.y >= _tiles.GetLength(1))
            return;

        playerMarker.anchoredPosition = GetCellCenterAnchoredInMarkerParent(cell);
        playerMarker.SetAsLastSibling();
    }

    private void ApplyPlayerMarkerSpriteFromPresentation()
    {
        if (playerMarkerImage == null)
            return;
        var sp = ResolvePlayerMarkerSprite();
        playerMarkerImage.sprite = sp;
        playerMarkerImage.enabled = sp != null;
    }

    Sprite ResolvePlayerMarkerSprite()
    {
        var playerData = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        if (playerData != null && playerData.MapPlayerIcon != null)
            return playerData.MapPlayerIcon;
        return _presentation != null ? _presentation.playerMarkerIcon : null;
    }

    private Vector2 GetCellCenterAnchoredInMarkerParent(Vector2Int cell)
    {
        var tileView = _tiles[cell.x, cell.y];
        var tileRt = tileView != null ? tileView.transform as RectTransform : null;
        if (tileRt == null || _markerParentRt == null)
            return playerMarker != null ? playerMarker.anchoredPosition : Vector2.zero;

        var worldCorners = new Vector3[4];
        tileRt.GetWorldCorners(worldCorners);
        var centerWorld = (worldCorners[0] + worldCorners[2]) * 0.5f;

        Camera cam = null;
        if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera)
            cam = _canvas.worldCamera;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _markerParentRt,
            RectTransformUtility.WorldToScreenPoint(cam, centerWorld),
            cam,
            out local);
        return local;
    }

    /// <summary>Updates per-tile “standing here” visuals (background, arrows) after <see cref="MapMovementManager.PlayerGridPosition"/> changes.</summary>
    public void RefreshPlayerStandingVisuals()
    {
        if (_manager == null || _tiles == null || _manager.Grid == null)
            return;

        var grid = _manager.Grid;
        var w = grid.Width;
        var h = grid.Height;
        if (_tiles.GetLength(0) != w || _tiles.GetLength(1) != h)
            return;

        var pStand = _standingVisualCellOverride ?? _manager.PlayerGridPosition;
        var pSelected = ResolveSelectedVisualCell(pStand);
        _manager.CollectReachableMoveTargetsFrom(_manager.PlayerGridPosition, _reachableMoveTargetsScratch);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var v = _tiles[x, y];
                if (v == null)
                    continue;

                var standingHere = pStand.x == x && pStand.y == y;
                var cell = new Vector2Int(x, y);
                MapTileUIViewState state;
                if (pSelected.x == x && pSelected.y == y)
                    state = MapTileUIViewState.Selected;
                else if (_reachableMoveTargetsScratch.Contains(cell))
                    state = MapTileUIViewState.Available;
                else
                    state = MapTileUIViewState.Idle;

                v.ApplyPlayerStandingAndReachabilityVisual(standingHere, state, allowBackgroundColorLerp: !_deferBackgroundColorLerpUntilFinalSegment);
            }
        }
    }

    Vector2Int ResolveSelectedVisualCell(Vector2Int standingVisualCell)
    {
        if (_tileStateSelectedCellOverride.HasValue)
            return _tileStateSelectedCellOverride.Value;
        if (_deferDestinationSelectedVisualUntilFinalSegment)
            return standingVisualCell;
        return _manager.PlayerGridPosition;
    }

    private void ClearMoveVisualOverrides()
    {
        _tileStateSelectedCellOverride = null;
        _standingVisualCellOverride = null;
        _deferBackgroundColorLerpUntilFinalSegment = false;
        _deferDestinationSelectedVisualUntilFinalSegment = false;
    }

    private void DetachPlayerMarkerBeforeClearingTiles()
    {
        if (playerMarker == null || tilesParent == null)
            return;

        Transform safe = tilesParent.parent;
        if (safe == null)
            safe = transform.parent;
        if (safe == null || safe == tilesParent)
            safe = playerMarker.root;

        playerMarker.SetParent(safe, false);
        _markerParentRt = playerMarker.parent as RectTransform;
        _canvas = playerMarker.GetComponentInParent<Canvas>();
    }

    private void ClearChildren()
    {
        if (tilesParent == null) return;
        for (var i = tilesParent.childCount - 1; i >= 0; i--)
            Destroy(tilesParent.GetChild(i).gameObject);
    }
}
