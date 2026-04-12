using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One map cell: background color, optional node label, exit arrows, click to move.</summary>
public class UIMapTileView : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button clickButton;
    [Header("Arrows (one GameObject per direction; orient sprites in the prefab)")]
    [SerializeField] private GameObject arrowTop;
    [SerializeField] private GameObject arrowRight;
    [SerializeField] private GameObject arrowBottom;
    [SerializeField] private GameObject arrowLeft;
    [Header("Arrow colors")]
    [SerializeField] private Color arrowColorCurrentTile = new Color(1f, 0.95f, 0.4f, 1f);
    [SerializeField] private Color arrowColorOtherTiles = new Color(0.55f, 0.55f, 0.6f, 0.9f);
    [Header("Tile colors")]
    [SerializeField] private Color startTileColor = new Color(0.2f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color bossTileColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color combatColor = new Color(0.35f, 0.35f, 0.4f, 1f);
    [SerializeField] private Color shopColor = new Color(0.45f, 0.55f, 0.75f, 1f);
    [SerializeField] private Color treasureColor = new Color(0.85f, 0.7f, 0.2f, 1f);
    [SerializeField] private Color mysteryColor = new Color(0.55f, 0.35f, 0.65f, 1f);
    [Header("Optional")]
    [SerializeField] private TMP_Text nodeTypeLabel;

    private Vector2Int _cell;
    private MapMovementManager _manager;
    private int _lastExitMask;
    private bool _playerOnThisTile;

    private void Awake()
    {
        if (backgroundImage == null)
            Debug.LogError("UIMapTileView: assign backgroundImage.", this);
        if (clickButton == null)
            Debug.LogError("UIMapTileView: assign clickButton.", this);
    }

    public void Setup(Vector2Int cell, MapTile tile, bool isStart, bool isBoss, MapMovementManager manager)
    {
        _cell = cell;
        _manager = manager;

        if (clickButton != null)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(OnClicked);
        }

        if (backgroundImage != null)
        {
            if (isStart)
                backgroundImage.color = startTileColor;
            else if (isBoss)
                backgroundImage.color = bossTileColor;
            else
                backgroundImage.color = ColorForNodeType(tile.nodeType);
        }

        if (nodeTypeLabel != null)
        {
            if (isStart)
                nodeTypeLabel.text = "Start";
            else if (isBoss)
                nodeTypeLabel.text = "Boss";
            else
                nodeTypeLabel.text = tile.nodeType.ToString();
        }

        _playerOnThisTile = manager != null && cell == manager.PlayerGridPosition;
        ApplyExitMask(tile.exitMask);
    }

    /// <summary>Updates arrow tint for “standing here” vs other tiles (call when the player moves).</summary>
    public void SetPlayerStandingHere(bool standingHere)
    {
        _playerOnThisTile = standingHere;
        ApplyExitMask(_lastExitMask);
    }

    public void RefreshExits(MapGrid grid)
    {
        if (grid == null || !grid.Contains(_cell))
            return;
        ApplyExitMask(grid.Get(_cell.x, _cell.y).exitMask);
    }

    private void ApplyExitMask(int exitMask)
    {
        _lastExitMask = exitMask;
        SetArrowVisual(arrowTop, MapCardinalDirection.Top);
        SetArrowVisual(arrowRight, MapCardinalDirection.Right);
        SetArrowVisual(arrowBottom, MapCardinalDirection.Bottom);
        SetArrowVisual(arrowLeft, MapCardinalDirection.Left);
    }

    private void SetArrowVisual(GameObject arrowGo, MapCardinalDirection direction)
    {
        if (arrowGo == null) return;
        var show = _lastExitMask.Contains(direction);
        arrowGo.SetActive(show);
        if (!show) return;

        var graphic = arrowGo.GetComponent<Graphic>() ?? arrowGo.GetComponentInChildren<Graphic>(true);
        if (graphic != null)
            graphic.color = _playerOnThisTile ? arrowColorCurrentTile : arrowColorOtherTiles;
    }

    private Color ColorForNodeType(MapNodeType t)
    {
        return t switch
        {
            MapNodeType.Combat => combatColor,
            MapNodeType.Shop => shopColor,
            MapNodeType.Treasure => treasureColor,
            MapNodeType.Mystery => mysteryColor,
            _ => combatColor
        };
    }

    private void OnClicked()
    {
        if (_manager != null)
            _manager.TryMoveTo(_cell);
    }
}
