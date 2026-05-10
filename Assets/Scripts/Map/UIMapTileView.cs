using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Reachability / selection state for map tile UI (drives colors and optional available pulse).</summary>
public enum MapTileUIViewState
{
    /// <summary>Not a valid one-step move from the player.</summary>
    Idle = 0,
    /// <summary>Orthogonal neighbor of the player with a directed exit from the player’s tile toward it.</summary>
    Available = 1,
    /// <summary>Tile the player is on, or the click target while the pawn is moving.</summary>
    Selected = 2
}

/// <summary>One map cell: background color, optional node label, event icon, exit arrows, click to move.</summary>
public class UIMapTileView : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button clickButton;
    [Header("Exit arrows (one GameObject per direction; orient sprites in the prefab)")]
    [FormerlySerializedAs("arrowTop")]
    [SerializeField] private GameObject exitArrowTop;
    [FormerlySerializedAs("arrowRight")]
    [SerializeField] private GameObject exitArrowRight;
    [FormerlySerializedAs("arrowBottom")]
    [SerializeField] private GameObject exitArrowBottom;
    [FormerlySerializedAs("arrowLeft")]
    [SerializeField] private GameObject exitArrowLeft;
    [Header("Incoming arrows (one GameObject per direction; optional)")]
    [SerializeField] private GameObject incomingArrowTop;
    [SerializeField] private GameObject incomingArrowRight;
    [SerializeField] private GameObject incomingArrowBottom;
    [SerializeField] private GameObject incomingArrowLeft;
    [Header("Arrow colors")]
    [SerializeField] private Color arrowColorCurrentTile = new Color(1f, 0.95f, 0.4f, 1f);
    [SerializeField] private Color arrowColorOtherTiles = new Color(0.55f, 0.55f, 0.6f, 0.9f);
    [Header("Tile background (standing only)")]
    [Tooltip("Background tint on the tile the pawn is standing on; other tiles use the default background color.")]
    [SerializeField] private Color playerCurrentTileBackgroundColor = new Color(0.85f, 0.85f, 0.4f, 1f);
    [Header("Event icon (reachability)")]
    [SerializeField] private Color iconColorAvailable = new Color(0.35f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color iconColorNotAvailable = Color.white;
    [Header("One-way exit arrow sizing")]
    [Tooltip("Used when this tile has an exit in a direction and the adjacent tile does not have a return exit back.")]
    [SerializeField, Min(1f)] private float oneWayExitArrowWidth = 60f;
    [Header("Optional")]
    [SerializeField] private TMP_Text nodeTypeLabel;
    [SerializeField] private Image eventIconImage;
    [Header("Visited tiles")]
    [Tooltip("Scale multiplier applied to background image when tile.eventConsumed is true, or when this is the start cell and the player has moved elsewhere.")]
    [SerializeField, Min(0.01f)] private float visitedBackgroundScale = 0.9f;
    [Header("Boss tile")]
    [Tooltip("Uniform scale multiplier for the event icon on the boss / end cell only.")]
    [SerializeField, Min(0.01f)] private float bossTileEventIconScale = 1.2f;
    [Header("Available tile pulse (optional)")]
    [Tooltip("Stays active; scale loops from the curve only while this tile is Available (otherwise scale resets to base).")]
    [SerializeField] private GameObject availablePulseTarget;
    [SerializeField] private AnimationCurve availablePulseScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float availablePulseLoopDurationSeconds = 1.2f;

    private Vector3 _eventIconBaseLocalScale = Vector3.one;
    private Vector3 _backgroundBaseLocalScale = Vector3.one;
    private Color _baseBackgroundColor = Color.white;
    private float _exitArrowTopBaseWidth;
    private float _exitArrowRightBaseWidth;
    private float _exitArrowBottomBaseWidth;
    private float _exitArrowLeftBaseWidth;
    private Vector2Int _cell;
    private MapMovementManager _manager;
    private int _lastExitMask;
    private int _lastIncomingMask;
    private int _lastOneWayExitMask;
    private bool _playerOnThisTile;
    /// <summary>Tile has an event icon sprite; hidden while <see cref="_playerOnThisTile"/>.</summary>
    private bool _eventIconActiveForTile;
    private bool _tileEventConsumed;
    private bool _isStartCell;
    private MapTileUIViewState _reachabilityState = MapTileUIViewState.Idle;
    private Transform _pulseTransform;
    private Vector3 _pulseBaseScale = Vector3.one;
    private float _pulsePhase;

    private void Awake()
    {
        if (backgroundImage == null)
            Debug.LogError("UIMapTileView: assign backgroundImage.", this);
        else
            _baseBackgroundColor = backgroundImage.color;
        if (clickButton == null)
            Debug.LogError("UIMapTileView: assign clickButton.", this);
        if (eventIconImage != null)
            _eventIconBaseLocalScale = eventIconImage.transform.localScale;
        if (backgroundImage != null)
            _backgroundBaseLocalScale = backgroundImage.transform.localScale;
        _exitArrowTopBaseWidth = ReadArrowWidth(exitArrowTop);
        _exitArrowRightBaseWidth = ReadArrowWidth(exitArrowRight);
        _exitArrowBottomBaseWidth = ReadArrowWidth(exitArrowBottom);
        _exitArrowLeftBaseWidth = ReadArrowWidth(exitArrowLeft);

        if (availablePulseTarget != null)
        {
            _pulseTransform = availablePulseTarget.transform;
            _pulseBaseScale = _pulseTransform.localScale;
            availablePulseTarget.SetActive(true);
        }
    }

    private void Update()
    {
        UpdateAvailablePulseAnimation();
    }

    /// <summary>Applies standing (pawn) highlight, reachability colors, and arrow tint in one step.</summary>
    public void ApplyPlayerStandingAndReachabilityVisual(bool standingHere, MapTileUIViewState reachabilityState)
    {
        _playerOnThisTile = standingHere;
        _reachabilityState = reachabilityState;
        ApplyBackgroundForStandingOnly();
        ApplyBackgroundScaleForVisited();
        ApplyEventIconVisibilityForStanding();
        ApplyEventIconReachabilityTint();
        ApplyArrowMasks(_lastExitMask, _lastIncomingMask, _lastOneWayExitMask);
        SyncAvailablePulseObjectActive();
    }

    public void Setup(Vector2Int cell, MapTile tile, bool isStart, bool isBoss, MapMovementManager manager,
        MapPresentationSO presentation)
    {
        _cell = cell;
        _manager = manager;
        _tileEventConsumed = tile.eventConsumed;
        _isStartCell = isStart;

        if (clickButton != null)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(OnClicked);
        }

        if (nodeTypeLabel != null)
        {
            if (tile.eventConsumed)
                nodeTypeLabel.text = "";
            else if (isStart)
                nodeTypeLabel.text = "Start";
            else if (isBoss)
                nodeTypeLabel.text = "Boss";
            else
                nodeTypeLabel.text = tile.eventType.ToString();
        }

        _eventIconActiveForTile = false;
        if (eventIconImage != null)
        {
            if (tile.eventConsumed)
            {
                _eventIconActiveForTile = false;
            }
            else if (presentation != null)
            {
                var sp = presentation.GetEventIcon(tile.eventType, isStart, isBoss);
                if (sp != null)
                {
                    eventIconImage.sprite = sp;
                    _eventIconActiveForTile = true;
                }
            }
        }

        if (eventIconImage != null)
        {
            if (_eventIconActiveForTile)
                eventIconImage.transform.localScale = _eventIconBaseLocalScale * (isBoss ? bossTileEventIconScale : 1f);
            else
                eventIconImage.transform.localScale = _eventIconBaseLocalScale;
        }

        _playerOnThisTile = manager != null && cell == manager.PlayerGridPosition;
        if (manager != null && manager.Grid != null && manager.Grid.Contains(cell))
        {
            var p = manager.PlayerGridPosition;
            if (cell == p)
                _reachabilityState = MapTileUIViewState.Selected;
            else if (manager.IsValidOneStepMoveTarget(cell))
                _reachabilityState = MapTileUIViewState.Available;
            else
                _reachabilityState = MapTileUIViewState.Idle;
        }
        else
            _reachabilityState = MapTileUIViewState.Idle;

        ApplyBackgroundForStandingOnly();
        ApplyBackgroundScaleForVisited();
        ApplyEventIconVisibilityForStanding();
        ApplyEventIconReachabilityTint();
        var incomingMask = 0;
        var oneWayExitMask = 0;
        var currentGrid = manager != null ? manager.Grid : null;
        if (currentGrid != null && currentGrid.Contains(_cell))
        {
            incomingMask = BuildIncomingMask(currentGrid);
            oneWayExitMask = BuildOneWayExitMask(currentGrid, tile.exitMask);
        }
        ApplyArrowMasks(tile.exitMask, incomingMask, oneWayExitMask);
        SyncAvailablePulseObjectActive();
    }

    /// <summary>Updates arrow tint for “standing here” vs other tiles (call when the player moves).</summary>
    public void SetPlayerStandingHere(bool standingHere)
    {
        _playerOnThisTile = standingHere;
        ApplyBackgroundForStandingOnly();
        ApplyBackgroundScaleForVisited();
        ApplyEventIconVisibilityForStanding();
        ApplyEventIconReachabilityTint();
        ApplyArrowMasks(_lastExitMask, _lastIncomingMask, _lastOneWayExitMask);
        SyncAvailablePulseObjectActive();
    }

    private void ApplyBackgroundForStandingOnly()
    {
        if (backgroundImage == null)
            return;
        backgroundImage.color = _playerOnThisTile ? playerCurrentTileBackgroundColor : _baseBackgroundColor;
    }

    private void ApplyEventIconVisibilityForStanding()
    {
        if (eventIconImage == null)
            return;
        if (!_eventIconActiveForTile)
        {
            eventIconImage.enabled = false;
            return;
        }

        eventIconImage.enabled = true;
    }

    private void ApplyEventIconReachabilityTint()
    {
        if (eventIconImage == null || !_eventIconActiveForTile || !eventIconImage.enabled)
            return;
        var available = _reachabilityState == MapTileUIViewState.Available;
        eventIconImage.color = available ? iconColorAvailable : iconColorNotAvailable;
    }

    public void RefreshExits(MapGrid grid)
    {
        if (grid == null || !grid.Contains(_cell))
            return;
        var exitMask = grid.Get(_cell.x, _cell.y).exitMask;
        var incomingMask = BuildIncomingMask(grid);
        var oneWayExitMask = BuildOneWayExitMask(grid, exitMask);
        ApplyArrowMasks(exitMask, incomingMask, oneWayExitMask);
    }

    private int BuildIncomingMask(MapGrid grid)
    {
        var mask = 0;
        for (var i = 0; i < 4; i++)
        {
            var direction = (MapCardinalDirection)i;
            var neighbor = _cell + direction.ToDelta();
            if (grid.Contains(neighbor) && grid.HasExit(neighbor.x, neighbor.y, direction.Opposite()))
                mask = mask.With(direction);
        }

        return mask;
    }

    private int BuildOneWayExitMask(MapGrid grid, int exitMask)
    {
        var mask = 0;
        for (var i = 0; i < 4; i++)
        {
            var direction = (MapCardinalDirection)i;
            if (!exitMask.Contains(direction))
                continue;

            var neighbor = _cell + direction.ToDelta();
            if (!grid.Contains(neighbor))
                continue;

            if (!grid.HasExit(neighbor.x, neighbor.y, direction.Opposite()))
                mask = mask.With(direction);
        }

        return mask;
    }

    private void ApplyArrowMasks(int exitMask, int incomingMask, int oneWayExitMask)
    {
        _lastExitMask = exitMask;
        _lastIncomingMask = incomingMask;
        _lastOneWayExitMask = oneWayExitMask;

        SetArrowVisual(exitArrowTop, MapCardinalDirection.Top, _lastExitMask, _lastOneWayExitMask, _exitArrowTopBaseWidth, true);
        SetArrowVisual(exitArrowRight, MapCardinalDirection.Right, _lastExitMask, _lastOneWayExitMask, _exitArrowRightBaseWidth, true);
        SetArrowVisual(exitArrowBottom, MapCardinalDirection.Bottom, _lastExitMask, _lastOneWayExitMask, _exitArrowBottomBaseWidth, true);
        SetArrowVisual(exitArrowLeft, MapCardinalDirection.Left, _lastExitMask, _lastOneWayExitMask, _exitArrowLeftBaseWidth, true);

        SetArrowVisual(incomingArrowTop, MapCardinalDirection.Top, _lastIncomingMask, 0, 0f, false);
        SetArrowVisual(incomingArrowRight, MapCardinalDirection.Right, _lastIncomingMask, 0, 0f, false);
        SetArrowVisual(incomingArrowBottom, MapCardinalDirection.Bottom, _lastIncomingMask, 0, 0f, false);
        SetArrowVisual(incomingArrowLeft, MapCardinalDirection.Left, _lastIncomingMask, 0, 0f, false);
    }

    private void SetArrowVisual(
        GameObject arrowGo,
        MapCardinalDirection direction,
        int arrowMask,
        int oneWayMask,
        float baseWidth,
        bool applyWidthOverride)
    {
        if (arrowGo == null) return;
        var show = arrowMask.Contains(direction);
        arrowGo.SetActive(show);
        if (applyWidthOverride)
            ApplyArrowWidth(arrowGo, show && oneWayMask.Contains(direction), baseWidth);
        if (!show) return;

        var graphic = arrowGo.GetComponent<Graphic>() ?? arrowGo.GetComponentInChildren<Graphic>(true);
        if (graphic != null)
            graphic.color = _playerOnThisTile ? arrowColorCurrentTile : arrowColorOtherTiles;
    }

    private float ReadArrowWidth(GameObject arrowGo)
    {
        var rt = arrowGo != null ? arrowGo.transform as RectTransform : null;
        return rt != null ? rt.sizeDelta.x : 0f;
    }

    private void ApplyArrowWidth(GameObject arrowGo, bool useOneWayWidth, float baseWidth)
    {
        var rt = arrowGo != null ? arrowGo.transform as RectTransform : null;
        if (rt == null)
            return;

        var size = rt.sizeDelta;
        size.x = useOneWayWidth ? oneWayExitArrowWidth : baseWidth;
        rt.sizeDelta = size;
    }

    private void ApplyBackgroundScaleForVisited()
    {
        if (backgroundImage == null)
            return;
        var leftStartVisuallyVisited = _isStartCell && !_playerOnThisTile;
        var useVisitedScale = _tileEventConsumed || leftStartVisuallyVisited;
        backgroundImage.transform.localScale = _playerOnThisTile
            ? Vector3.one
            : useVisitedScale
            ? _backgroundBaseLocalScale * visitedBackgroundScale
            : _backgroundBaseLocalScale;
    }

    private void SyncAvailablePulseObjectActive()
    {
        if (availablePulseTarget == null || _pulseTransform == null)
            return;
        availablePulseTarget.SetActive(true);
        var pulsing = !_playerOnThisTile && _reachabilityState == MapTileUIViewState.Available;
        if (!pulsing)
        {
            _pulseTransform.localScale = _pulseBaseScale;
            _pulsePhase = 0f;
        }
    }

    private void UpdateAvailablePulseAnimation()
    {
        if (availablePulseTarget == null || _pulseTransform == null)
            return;
        if (_playerOnThisTile || _reachabilityState != MapTileUIViewState.Available)
            return;

        var period = availablePulseLoopDurationSeconds;
        _pulsePhase += Time.unscaledDeltaTime / period;
        if (_pulsePhase > 1f)
            _pulsePhase -= Mathf.Floor(_pulsePhase);

        var curve = availablePulseScaleCurve != null && availablePulseScaleCurve.length > 0
            ? availablePulseScaleCurve
            : AnimationCurve.Constant(0f, 1f, 1f);
        var mult = curve.Evaluate(_pulsePhase);
        _pulseTransform.localScale = _pulseBaseScale * mult;
    }

    private void OnClicked()
    {
        if (_manager != null)
            _manager.TryMoveTo(_cell);
    }
}
