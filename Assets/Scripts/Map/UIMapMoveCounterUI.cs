using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays remaining map moves (countdown) and tints red when over limit. Use <see cref="movesTextFormat"/> with <c>{0}</c> = moves left.</summary>
public class UIMapMoveCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text movesText;
    [Tooltip("String.Format pattern. {0} = moves remaining before the limit (never negative).")]
    [SerializeField] private string movesTextFormat = "{0}";
    [SerializeField] private string unboundMovesText = "—";
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color overLimitColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    [Header("Optional hover tooltip")]
    [Tooltip("Graphic that receives pointer hover (e.g. full-rect Image behind the moves text).")]
    [SerializeField] private Graphic tooltipPointerTarget;
    [Tooltip("Screen-space offset for the hover panel relative to the tooltip target.")]
    [SerializeField] private Vector2 hoverTooltipScreenOffset = new Vector2(0f, 24f);
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
            movesText.text = unboundMovesText ?? "—";
            movesText.color = normalColor;
            return;
        }

        var taken = _manager.MovesTaken;
        var limit = _manager.MoveLimit;
        var movesLeft = Mathf.Max(0, limit - taken);
        var format = string.IsNullOrEmpty(movesTextFormat) ? "{0}" : movesTextFormat;
        try
        {
            movesText.text = string.Format(format, movesLeft);
        }
        catch (System.FormatException)
        {
            movesText.text = movesLeft.ToString();
            Debug.LogError($"UIMapMoveCounterUI: invalid movesTextFormat '{format}'. Use {{0}} for moves left.", this);
        }

        movesText.color = taken > limit ? overLimitColor : normalColor;
    }

    private void SetupHoverTooltipAfterBind()
    {
        if (tooltipPointerTarget == null)
            return;
        if (string.IsNullOrWhiteSpace(hoverTooltipTitle) && string.IsNullOrWhiteSpace(hoverTooltipDescription))
            return;

        var go = tooltipPointerTarget.gameObject;
        var legacy = go.GetComponent<HoverTooltipTargetUI>();
        if (legacy != null)
            Destroy(legacy);

        var dynamicTip = go.GetComponent<UIMapMovesHoverTooltip>();
        if (dynamicTip == null)
            dynamicTip = go.AddComponent<UIMapMovesHoverTooltip>();
        var anchor = tooltipPointerTarget.rectTransform;
        dynamicTip.Initialize(anchor, _manager, hoverTooltipTitle, hoverTooltipDescription, hoverTooltipScreenOffset);
    }
}
