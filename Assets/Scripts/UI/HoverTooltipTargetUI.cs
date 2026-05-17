using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to any UI object that should show the shared hover tooltip via <see cref="HoverTooltipManager"/>.
/// </summary>
public class HoverTooltipTargetUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string tooltipTitle;
    [SerializeField, TextArea] private string tooltipDescription;
    [SerializeField] private Vector2 tooltipScreenOffset;
    [Tooltip("When true, HoverTooltipManager uses its Hover Above Tooltip Screen Offset instead of Tooltip Screen Offset.")]
    [SerializeField] private bool isAbove;
    private Sprite _tooltipBackground;
    private ScriptableObject _scriptableSource;
    private bool _isPointerInside;
    private bool _warnedMissingManager;

    /// <summary>When set, hover uses <see cref="HoverTooltipManager.TryGetTooltipContent"/> instead of manual title/description.</summary>
    public void SetScriptableSource(ScriptableObject source) => _scriptableSource = source;

    public void SetContent(string title, string description, Sprite tooltipBackground = null)
    {
        tooltipTitle = title ?? string.Empty;
        tooltipDescription = description ?? string.Empty;
        _tooltipBackground = tooltipBackground;
    }

    public void SetTooltipScreenOffset(Vector2 screenOffset) => tooltipScreenOffset = screenOffset;

    public void SetIsAbove(bool above) => isAbove = above;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerInside = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerInside = false;
        HoverTooltipManager.HideAllTooltipPanels();
    }

    private void OnDisable()
    {
        _isPointerInside = false;
        HoverTooltipManager.HideAllTooltipPanels();
    }

    private void Update()
    {
        if (!_isPointerInside)
            return;
        ShowTooltip();
    }

    private void ShowTooltip()
    {
        var anchor = transform as RectTransform;
        if (anchor == null)
            return;

        var mgr = HoverTooltipManager.Instance;
        if (mgr == null || !mgr.HasValidPrefab)
        {
            if (!_warnedMissingManager)
            {
                _warnedMissingManager = true;
                Debug.LogError(
                    "HoverTooltipTargetUI: add an enabled HoverTooltipManager with panelPrefab to the scene (or a persistent bootstrap).",
                    this);
            }

            return;
        }

        if (_scriptableSource != null)
        {
            if (!HoverTooltipManager.TryGetTooltipContent(_scriptableSource, out var t, out var d, out var bg))
                return;
            if (string.IsNullOrWhiteSpace(t) && string.IsNullOrWhiteSpace(d))
                return;
            mgr.Show(anchor, tooltipScreenOffset, t, d, bg, isAbove);
            return;
        }

        if (string.IsNullOrWhiteSpace(tooltipTitle) && string.IsNullOrWhiteSpace(tooltipDescription))
            return;

        mgr.Show(anchor, tooltipScreenOffset, tooltipTitle, tooltipDescription, _tooltipBackground, isAbove);
    }
}
