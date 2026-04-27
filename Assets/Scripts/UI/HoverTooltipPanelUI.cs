using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared hover tooltip panel for UI icons (status, element pool, etc.).
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
}
