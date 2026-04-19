using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

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
    [Header("Tile highlight")]
    [SerializeField] private Color playerCurrentTileBackgroundColor = new Color(0.85f, 0.85f, 0.4f, 1f);
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
                    eventIconImage.color = Color.white;
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
        ApplyBackgroundColorForStanding();
        ApplyBackgroundScaleForVisited();
        ApplyEventIconVisibilityForStanding();
        var incomingMask = 0;
        var oneWayExitMask = 0;
        var currentGrid = manager != null ? manager.Grid : null;
        if (currentGrid != null && currentGrid.Contains(_cell))
        {
            incomingMask = BuildIncomingMask(currentGrid);
            oneWayExitMask = BuildOneWayExitMask(currentGrid, tile.exitMask);
        }
        ApplyArrowMasks(tile.exitMask, incomingMask, oneWayExitMask);
    }

    /// <summary>Updates arrow tint for “standing here” vs other tiles (call when the player moves).</summary>
    public void SetPlayerStandingHere(bool standingHere)
    {
        _playerOnThisTile = standingHere;
        ApplyBackgroundColorForStanding();
        ApplyBackgroundScaleForVisited();
        ApplyEventIconVisibilityForStanding();
        ApplyArrowMasks(_lastExitMask, _lastIncomingMask, _lastOneWayExitMask);
    }

    private void ApplyBackgroundColorForStanding()
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

        eventIconImage.enabled = !_playerOnThisTile;
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

    private void OnClicked()
    {
        if (_manager != null)
            _manager.TryMoveTo(_cell);
    }
}
