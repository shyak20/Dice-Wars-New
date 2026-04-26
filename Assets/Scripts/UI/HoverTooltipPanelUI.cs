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
