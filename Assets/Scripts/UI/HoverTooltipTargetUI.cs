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

    public void SetContent(string title, string description)
    {
        tooltipTitle = title ?? string.Empty;
        tooltipDescription = description ?? string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(tooltipTitle) && string.IsNullOrWhiteSpace(tooltipDescription))
            return;

        var panel = ResolvePanel();
        if (panel == null) return;
        panel.Show(tooltipTitle, tooltipDescription);
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
