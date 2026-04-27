using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to any UI object that should show the shared hover tooltip.
/// </summary>
public class HoverTooltipTargetUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string tooltipTitle;
    [SerializeField, TextArea] private string tooltipDescription;
    [SerializeField] private HoverTooltipPanelUI tooltipPanel;
    private Sprite _tooltipBackground;

    public void SetContent(string title, string description, Sprite tooltipBackground = null)
    {
        tooltipTitle = title ?? string.Empty;
        tooltipDescription = description ?? string.Empty;
        _tooltipBackground = tooltipBackground;
    }

    /// <summary>Optional panel wiring for runtime setup (e.g. <see cref="UIMapMoveCounterUI"/>).</summary>
    public void Configure(HoverTooltipPanelUI panel, string title, string description, Sprite tooltipBackground = null)
    {
        if (panel != null)
            tooltipPanel = panel;
        SetContent(title, description, tooltipBackground);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(tooltipTitle) && string.IsNullOrWhiteSpace(tooltipDescription))
            return;

        var panel = ResolvePanel();
        if (panel == null) return;
        panel.Show(tooltipTitle, tooltipDescription, _tooltipBackground);
        panel.AlignPivotWorldXToRect(transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var panel = ResolvePanel();
        if (panel == null) return;
        panel.Hide();
    }

    private HoverTooltipPanelUI ResolvePanel()
    {
        if (tooltipPanel != null) return tooltipPanel;

        tooltipPanel = FindObjectOfType<HoverTooltipPanelUI>(true);
        return tooltipPanel;
    }

    private void OnDisable()
    {
        var panel = tooltipPanel;
        if (panel != null)
            panel.Hide();
    }
}
