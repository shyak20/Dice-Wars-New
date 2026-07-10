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

    /// <summary>
    /// True after an explicit <see cref="Show"/>. The panel object may be saved inactive in its prefab, in which
    /// case Awake runs lazily inside the first Show's SetActive(true); without this flag that deferred Awake
    /// would call Hide and wipe the very first tooltip.
    /// </summary>
    private bool _shown;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
        EnsurePanelDoesNotBlockRaycasts();
        if (!_shown)
            Hide();
    }

    /// <summary>Active visual root (position/size target for placement, clamping, and sorting).</summary>
    public RectTransform PanelRect => Root.transform as RectTransform;

    GameObject Root => panelRoot != null ? panelRoot : gameObject;

    public void Show(string title, string description, Sprite tooltipBackground = null)
    {
        if (titleText != null) titleText.text = title ?? string.Empty;
        if (descriptionText != null) descriptionText.text = description ?? string.Empty;
        if (tooltipBackgroundImage != null)
        {
            tooltipBackgroundImage.sprite = tooltipBackground;
            tooltipBackgroundImage.enabled = tooltipBackground != null;
        }

        _shown = true;
        Root.SetActive(true);
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
    /// Aligns tooltip pivot to the reference rect center, then applies offset in canvas parent local space.
    /// </summary>
    public void AlignToRectWithScreenOffset(RectTransform reference, Vector2 screenOffset)
    {
        if (reference == null || panelRoot == null)
            return;

        var panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null)
            return;

        HoverTooltipLayoutUtility.AlignPanelPivotToRectCenterWithLocalOffset(panelRect, reference, screenOffset);
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
