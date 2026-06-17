using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hover tooltip on the fight-scene Roll button showing Perfect Cast and Cast Overload odds for the current dice selection.
/// </summary>
public sealed class RollButtonCastOddsHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private CombatUIController combatUi;
    [Tooltip("When true, uses HoverTooltipManager Hover Above Tooltip Screen Offset (see manager on the scene).")]
    [SerializeField] private bool isAbove = true;
    [SerializeField] private Vector2 tooltipScreenOffset;

    private RectTransform _anchorRect;
    private bool _isPointerInside;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (combatManager == null)
            combatManager = FindObjectOfType<CombatManager>();
        if (combatUi == null)
            combatUi = FindObjectOfType<CombatUIController>();
    }
#endif

    private void Awake()
    {
        _anchorRect = transform as RectTransform;
        if (combatManager == null)
            combatManager = FindObjectOfType<CombatManager>();
        if (combatUi == null)
            combatUi = FindObjectOfType<CombatUIController>();
    }

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
        if (_isPointerInside)
            ShowTooltip();
    }

    private void ShowTooltip()
    {
        if (_anchorRect == null || combatManager == null || combatUi == null)
            return;
        if (combatManager.GetCombatState() != CombatState.WaitingForRoll)
            return;
        if (!combatUi.TryGetRollCastOddsInput(out var selectedDice))
            return;
        if (!combatManager.TryComputeRollCastOdds(selectedDice, out var perfect, out var bust))
            return;

        var mgr = HoverTooltipManager.Instance;
        if (mgr == null || !mgr.HasValidPrefab)
            return;

        var description =
            $"Perfect Cast: {FormatPercent(perfect)}\nCast Overload: {FormatPercent(bust)}";
        mgr.Show(_anchorRect, tooltipScreenOffset, "Probabilities", description, isAbove: isAbove);
    }

    private static string FormatPercent(float percent) => $"{Mathf.RoundToInt(percent)}%";
}
