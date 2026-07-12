using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover tooltip above the player HP bar showing predicted HP loss from the upcoming enemy round
/// (attacks + current Burn/Poison + Burn/Poison enemies will apply).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class PlayerIncomingDamageHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CombatManager combatManager;
    [Tooltip("When true, uses HoverTooltipManager Hover Above Tooltip Screen Offset.")]
    [SerializeField] private bool isAbove = true;
    [SerializeField] private Vector2 tooltipScreenOffset;

    RectTransform _anchorRect;
    bool _isPointerInside;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (combatManager == null)
            combatManager = FindObjectOfType<CombatManager>();
    }
#endif

    void Awake()
    {
        _anchorRect = transform as RectTransform;
        if (combatManager == null)
            combatManager = FindObjectOfType<CombatManager>();

        EnsureRaycastGraphic();
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

    void OnDisable()
    {
        _isPointerInside = false;
        HoverTooltipManager.HideAllTooltipPanels();
    }

    void Update()
    {
        if (_isPointerInside)
            ShowTooltip();
    }

    void ShowTooltip()
    {
        if (_anchorRect == null)
            return;

        if (combatManager == null)
            combatManager = FindObjectOfType<CombatManager>();
        if (combatManager == null)
            return;

        if (!IsTooltipState(combatManager.GetCombatState()))
        {
            HoverTooltipManager.HideAllTooltipPanels();
            return;
        }

        if (!combatManager.TryPreviewIncomingPlayerDamage(out var preview))
            return;

        var mgr = HoverTooltipManager.Instance;
        if (mgr == null || !mgr.HasValidPrefab)
            return;

        mgr.Show(
            _anchorRect,
            tooltipScreenOffset,
            "Incoming Damage",
            preview.FormatTooltipDescription(),
            isAbove: isAbove);
    }

    static bool IsTooltipState(CombatState state)
    {
        return state == CombatState.WaitingForRoll
               || state == CombatState.EnemyTurnIntro;
    }

    void EnsureRaycastGraphic()
    {
        var graphic = GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
            return;
        }

        var image = gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
    }
}
