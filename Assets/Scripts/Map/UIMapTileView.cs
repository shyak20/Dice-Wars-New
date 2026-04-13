using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One map cell: background color, optional node label, event icon, exit arrows, click to move.</summary>
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
    [SerializeField] private Color combatNormalColor = new Color(0.35f, 0.35f, 0.4f, 1f);
    [SerializeField] private Color combatEliteColor = new Color(0.65f, 0.35f, 0.55f, 1f);
    [SerializeField] private Color combatBossColor = new Color(0.55f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color shopColor = new Color(0.45f, 0.55f, 0.75f, 1f);
    [SerializeField] private Color unknownColor = new Color(0.45f, 0.45f, 0.5f, 1f);
    [SerializeField] private Color shrineColor = new Color(0.55f, 0.75f, 0.5f, 1f);
    [SerializeField] private Color noneTileColor = new Color(0.32f, 0.34f, 0.38f, 1f);
    [Header("Optional")]
    [SerializeField] private TMP_Text nodeTypeLabel;
    [SerializeField] private Image eventIconImage;

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

    public void Setup(Vector2Int cell, MapTile tile, bool isStart, bool isBoss, MapMovementManager manager,
        MapPresentationSO presentation)
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
                backgroundImage.color = ColorForEventType(tile.eventType);
        }

        if (nodeTypeLabel != null)
        {
            if (tile.eventConsumed)
                nodeTypeLabel.text = presentation != null && presentation.visitedTileIcon != null ? "" : "V";
            else if (isStart)
                nodeTypeLabel.text = "Start";
            else if (isBoss)
                nodeTypeLabel.text = "Boss";
            else
                nodeTypeLabel.text = tile.eventType.ToString();
        }

        if (eventIconImage != null)
        {
            if (tile.eventConsumed)
            {
                if (presentation != null && presentation.visitedTileIcon != null)
                {
                    eventIconImage.sprite = presentation.visitedTileIcon;
                    eventIconImage.enabled = true;
                }
                else
                    eventIconImage.enabled = false;
            }
            else if (presentation != null)
            {
                var sp = presentation.GetEventIcon(tile.eventType, isStart, isBoss);
                if (sp != null)
                {
                    eventIconImage.sprite = sp;
                    eventIconImage.enabled = true;
                }
                else
                    eventIconImage.enabled = false;
            }
            else
                eventIconImage.enabled = false;
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

    private Color ColorForEventType(MapEventType t)
    {
        return t switch
        {
            MapEventType.CombatNormal => combatNormalColor,
            MapEventType.CombatElite => combatEliteColor,
            MapEventType.CombatBoss => combatBossColor,
            MapEventType.Shop => shopColor,
            MapEventType.Unknown => unknownColor,
            MapEventType.Shrine => shrineColor,
            MapEventType.None => noneTileColor,
            _ => combatNormalColor
        };
    }

    private void OnClicked()
    {
        if (_manager != null)
            _manager.TryMoveTo(_cell);
    }
}
