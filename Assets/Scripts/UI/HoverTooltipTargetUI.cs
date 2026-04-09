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
    [Header("Optional Prefab Spawn")]
    [Tooltip("If assigned, this prefab is instantiated and used as the tooltip panel.")]
    [SerializeField] private HoverTooltipPanelUI tooltipPanelPrefab;
    [Tooltip("Parent for spawned tooltip prefab. If empty, root canvas is used.")]
    [SerializeField] private RectTransform tooltipSpawnParent;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(24f, 16f);

    private HoverTooltipPanelUI spawnedTooltipPanel;

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
        PositionTooltip(panel);
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
        if (spawnedTooltipPanel != null) return spawnedTooltipPanel;
        if (tooltipPanel != null) return tooltipPanel;

        if (tooltipPanelPrefab != null)
        {
            var parent = ResolveSpawnParent();
            if (parent == null)
            {
                Debug.LogError($"HoverTooltipTargetUI on '{name}': No canvas found for tooltip prefab spawn.");
                return null;
            }

            spawnedTooltipPanel = Instantiate(tooltipPanelPrefab, parent);
            spawnedTooltipPanel.name = $"{tooltipPanelPrefab.name}_Runtime";
            return spawnedTooltipPanel;
        }

        tooltipPanel = FindObjectOfType<HoverTooltipPanelUI>(true);
        return tooltipPanel;
    }

    private RectTransform ResolveSpawnParent()
    {
        if (tooltipSpawnParent != null) return tooltipSpawnParent;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        return canvas.rootCanvas != null ? canvas.rootCanvas.transform as RectTransform : canvas.transform as RectTransform;
    }

    private void PositionTooltip(HoverTooltipPanelUI panel)
    {
        if (panel == null) return;

        var panelRect = panel.transform as RectTransform;
        var parentRect = panelRect != null ? panelRect.parent as RectTransform : null;
        if (panelRect == null || parentRect == null) return;

        var hoverRect = transform as RectTransform;
        if (hoverRect == null) return;

        var canvas = GetComponentInParent<Canvas>();
        var cameraForUi = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        var hoverScreenPos = RectTransformUtility.WorldToScreenPoint(cameraForUi, hoverRect.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, hoverScreenPos, cameraForUi, out var localPoint))
            panelRect.anchoredPosition = localPoint + tooltipOffset;
    }

    private void OnDisable()
    {
        var panel = spawnedTooltipPanel != null ? spawnedTooltipPanel : tooltipPanel;
        if (panel != null)
            panel.Hide();
    }
}
