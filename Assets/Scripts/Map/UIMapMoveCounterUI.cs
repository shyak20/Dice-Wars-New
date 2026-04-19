using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays "Moves: current/limit" and tints red when over limit.</summary>
public class UIMapMoveCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color overLimitColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    [Header("Optional hover tooltip")]
    [Tooltip("Graphic that receives pointer hover (e.g. full-rect Image behind the moves text).")]
    [SerializeField] private Graphic tooltipPointerTarget;
    [Tooltip("Leave empty to use the first HoverTooltipPanelUI in the scene.")]
    [SerializeField] private HoverTooltipPanelUI hoverTooltipPanel;
    [SerializeField] private string hoverTooltipTitle = "Moves";
    [SerializeField, TextArea] private string hoverTooltipDescription = "";

    private MapMovementManager _manager;

    public void Bind(MapMovementManager manager)
    {
        _manager = manager;
        Refresh();
        SetupHoverTooltipAfterBind();
    }

    public void Refresh()
    {
        if (movesText == null)
            return;

        if (_manager == null)
        {
            movesText.text = "Moves: —";
            movesText.color = normalColor;
            return;
        }

        var taken = _manager.MovesTaken;
        var limit = _manager.MoveLimit;
        movesText.text = $"Moves: {taken}/{limit}";
        movesText.color = taken > limit ? overLimitColor : normalColor;
    }

    private void SetupHoverTooltipAfterBind()
    {
        if (tooltipPointerTarget == null)
            return;
        if (string.IsNullOrWhiteSpace(hoverTooltipTitle) && string.IsNullOrWhiteSpace(hoverTooltipDescription))
            return;

        var panel = hoverTooltipPanel != null ? hoverTooltipPanel : FindObjectOfType<HoverTooltipPanelUI>(true);
        if (panel == null)
        {
            Debug.LogError("UIMapMoveCounterUI: hover tooltip text is set but no HoverTooltipPanelUI exists in the scene.", this);
            return;
        }

        var go = tooltipPointerTarget.gameObject;
        var legacy = go.GetComponent<HoverTooltipTargetUI>();
        if (legacy != null)
            Destroy(legacy);

        var dynamicTip = go.GetComponent<UIMapMovesHoverTooltip>();
        if (dynamicTip == null)
            dynamicTip = go.AddComponent<UIMapMovesHoverTooltip>();
        dynamicTip.Initialize(panel, _manager, hoverTooltipTitle, hoverTooltipDescription);
    }
}
