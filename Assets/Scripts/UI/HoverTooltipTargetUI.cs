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
    private PlayerTrialSO _trialSource;
    private TrialSaveData _trialState;
    private bool _warnedMissingManager;
    private bool _includeFaceHeaderTooltip;

    /// <summary>When set, hover uses <see cref="TooltipContentResolver"/> instead of manual title/description.</summary>
    public void SetScriptableSource(ScriptableObject source)
    {
        _scriptableSource = source;
        _trialSource = null;
    }

    public void SetTrialTooltip(PlayerTrialSO trial, TrialSaveData state)
    {
        _scriptableSource = null;
        _trialSource = trial;
        _trialState = state;
        tooltipTitle = string.Empty;
        tooltipDescription = string.Empty;
        _tooltipBackground = null;
    }

    public void SetContent(string title, string description, Sprite tooltipBackground = null)
    {
        _scriptableSource = null;
        _trialSource = null;
        tooltipTitle = title ?? string.Empty;
        tooltipDescription = description ?? string.Empty;
        _tooltipBackground = tooltipBackground;
    }

    public void SetTooltipScreenOffset(Vector2 screenOffset) => tooltipScreenOffset = screenOffset;

    public void SetIsAbove(bool above) => isAbove = above;

    /// <summary>
    /// When true and the source is a <see cref="DieFaceSO"/>, the face name/description is shown as the main tooltip
    /// (with the status/effect explanation stacked under it). Use where the hovered element does not already show them
    /// (Die Tooltip grid, face-replace screen).
    /// </summary>
    public void SetFaceHeaderTooltipEnabled(bool enabled) => _includeFaceHeaderTooltip = enabled;

    // Position is set once when the pointer enters and never updated afterwards, so the tooltip stays anchored
    // where it first appeared and does not drift if the hovered element moves or the pointer nudges over it.
    public void OnPointerEnter(PointerEventData eventData) => ShowTooltip();

    public void OnPointerExit(PointerEventData eventData) => HoverTooltipManager.HideAllTooltipPanels();

    private void OnDisable() => HoverTooltipManager.HideAllTooltipPanels();

    private void ShowTooltip()
    {
        var anchor = transform as RectTransform;
        if (anchor == null)
            return;

        var mgr = HoverTooltipManager.Instance;
        if (mgr == null)
        {
            LogMissingManagerOnce();
            return;
        }

        if (_trialSource != null)
        {
            if (!mgr.HasValidTrialRewardsPrefab)
            {
                LogMissingManagerOnce();
                return;
            }

            mgr.ShowTrialRewards(anchor, tooltipScreenOffset, _trialSource, _trialState, isAbove);
            return;
        }

        if (!mgr.HasValidPrefab)
        {
            LogMissingManagerOnce();
            return;
        }

        if (_scriptableSource != null)
        {
            mgr.ShowForScriptableObject(anchor, tooltipScreenOffset, _scriptableSource, isAbove, _includeFaceHeaderTooltip);
            return;
        }

        if (string.IsNullOrWhiteSpace(tooltipTitle) && string.IsNullOrWhiteSpace(tooltipDescription))
            return;

        mgr.Show(anchor, tooltipScreenOffset, tooltipTitle, tooltipDescription, _tooltipBackground, isAbove);
    }

    void LogMissingManagerOnce()
    {
        if (_warnedMissingManager)
            return;

        _warnedMissingManager = true;
        Debug.LogError(
            "HoverTooltipTargetUI: add an enabled HoverTooltipManager with panelPrefab and/or trialRewardsPanelPrefab.",
            this);
    }
}
