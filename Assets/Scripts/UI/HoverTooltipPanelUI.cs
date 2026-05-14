using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual root for hover text. Instantiate only via <see cref="HoverTooltipManager"/> (do not place in scenes as the primary tooltip).
/// </summary>
public class HoverTooltipPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("Optional. When set, Show applies a per-tooltip sprite passed into Show().")]
    [SerializeField] private Image tooltipBackgroundImage;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
        EnsurePanelDoesNotBlockRaycasts();
        Hide();
    }

    public void Show(string title, string description, Sprite tooltipBackground = null)
    {
        if (titleText != null) titleText.text = title ?? string.Empty;
        if (descriptionText != null) descriptionText.text = description ?? string.Empty;
        if (tooltipBackgroundImage != null)
        {
            tooltipBackgroundImage.sprite = tooltipBackground;
            tooltipBackgroundImage.enabled = tooltipBackground != null;
        }

        if (panelRoot != null) panelRoot.SetActive(true);
    }

    /// <summary>Aligns panel pivot world X to the reference rect center (preserves Y/Z).</summary>
    public void AlignPivotWorldXToRect(RectTransform reference)
    {
        if (reference == null || panelRoot == null) return;
        var panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null) return;

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);
        var centerWorldX = (corners[0].x + corners[2].x) * 0.5f;
        var pos = panelRect.position;
        pos.x = centerWorldX;
        panelRect.position = pos;
    }

    /// <summary>
    /// Aligns tooltip to the reference rect center, then applies screen-space pixel offset.
    /// </summary>
    public void AlignToRectWithScreenOffset(RectTransform reference, Vector2 screenOffset)
    {
        if (reference == null || panelRoot == null) return;
        var panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null) return;

        var parentRect = panelRect.parent as RectTransform;
        if (parentRect == null) return;

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);
        var centerWorld = (corners[0] + corners[2]) * 0.5f;

        var parentCanvas = GetComponentInParent<Canvas>();
        var cameraForCanvas = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;
        var screen = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, centerWorld) + screenOffset;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screen, cameraForCanvas, out var world))
            panelRect.position = world;
    }

    public void Hide()
    {
        if (titleText != null) titleText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (tooltipBackgroundImage != null)
        {
            tooltipBackgroundImage.sprite = null;
            tooltipBackgroundImage.enabled = false;
        }

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void EnsurePanelDoesNotBlockRaycasts()
    {
        if (panelRoot == null) return;
        var graphics = panelRoot.GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }
}
