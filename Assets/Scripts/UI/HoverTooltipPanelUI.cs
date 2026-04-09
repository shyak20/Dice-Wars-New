using TMPro;
using UnityEngine;

/// <summary>
/// Shared hover tooltip panel for UI icons (status, element pool, etc.).
/// </summary>
public class HoverTooltipPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
        Hide();
    }

    public void Show(string title, string description)
    {
        if (titleText != null) titleText.text = title ?? string.Empty;
        if (descriptionText != null) descriptionText.text = description ?? string.Empty;
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (titleText != null) titleText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
