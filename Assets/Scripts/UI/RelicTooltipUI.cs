using TMPro;
using UnityEngine;

/// <summary>
/// Scene UI singleton: shows <see cref="RelicSO.title"/> and <see cref="RelicSO.description"/>.
/// Horizontal position matches the hover target’s center (same rule as <see cref="HoverTooltipPanelUI.AlignPivotWorldXToRect"/>).
/// </summary>
public sealed class RelicTooltipUI : MonoBehaviour
{
    public static RelicTooltipUI Instance { get; private set; }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"RelicTooltipUI: duplicate on '{name}' — remove the extra. Only one per scene is supported.", this);
            enabled = false;
            return;
        }

        if (transform as RectTransform == null)
        {
            Debug.LogError("RelicTooltipUI: must be on a RectTransform.", this);
            enabled = false;
            return;
        }

        if (GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError("RelicTooltipUI: must be a child of a Canvas.", this);
            enabled = false;
            return;
        }

        if (panelRoot == null || titleText == null || descriptionText == null)
        {
            Debug.LogError("RelicTooltipUI: assign panelRoot, titleText, and descriptionText.", this);
            enabled = false;
            return;
        }

        Instance = this;
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable() => Hide();

    /// <param name="alignTo">Graphic that was hovered; panel pivot world X matches this rect’s horizontal center.</param>
    public void Show(RelicSO relic, RectTransform alignTo)
    {
        if (relic == null || panelRoot == null || titleText == null || descriptionText == null)
            return;

        titleText.text = relic.title ?? "";
        descriptionText.text = relic.description ?? "";
        panelRoot.SetActive(true);
        AlignPivotWorldXToReference(alignTo);
    }

    /// <summary>Aligns <see cref="panelRoot"/> pivot world X to <paramref name="reference"/> center (preserves Y/Z).</summary>
    public void AlignPivotWorldXToReference(RectTransform reference)
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
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
