using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;
    [Tooltip("Optional root for the stack badge visuals (text/background). Hidden when stacks are 0.")]
    [SerializeField] private GameObject stackVisualRoot;
    [Header("Tooltip")]
    [Tooltip("Tooltip offset in screen pixels relative to the hovered status icon.")]
    [SerializeField] private Vector2 tooltipScreenOffset = new Vector2(0f, 24f);
    [Tooltip("When on, tooltips use HoverTooltipManager's Hover Above Tooltip Screen Offset instead of the regular tooltip offset.")]
    [SerializeField] private bool showTooltipAbove;

    private HoverTooltipTargetUI hoverTooltipTarget;
    private Image _hitAreaImage;
    private bool _hoverTargetsInitialized;

    private void Awake()
    {
        if (iconImage == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': iconImage is not assigned!");
        if (stackText == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': stackText is not assigned!");

        EnsureHoverTooltipTargets();
    }

    public void Setup(StatusEffectInstance effect)
    {
        EnsureHoverTooltipTargets();
        RefreshVisual(effect);
    }

    public void SetupCustom(Sprite icon, string title, string description, Sprite tooltipBackground = null)
    {
        EnsureHoverTooltipTargets();

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.raycastTarget = false;
        }

        ApplyTooltipContent(title, description, tooltipBackground);
        UpdateStacks(0);
    }

    public void RefreshVisual(StatusEffectInstance effect)
    {
        EnsureHoverTooltipTargets();

        if (effect == null || effect.Definition == null) return;
        var spr = GameIconCatalog.GetStatusIcon(effect.Definition);
        if (spr == null && effect.Definition is BurnEffectSO)
            spr = GameIconCatalog.GetElementIcon(DieType.Fire);
        if (spr == null && effect.Definition is EchoEffectSO)
            spr = GameIconCatalog.GetElementIcon(DieType.Armor);
        if (spr == null && effect.Definition is ImmuneEffectSO)
            spr = GameIconCatalog.GetElementIcon(DieType.Ice);
        if (spr == null && effect.Definition is ThornsEffectSO)
            spr = GameIconCatalog.GetActionIcon(ActionVisualId.Thorns);
        if (spr == null && effect.Definition is NextTurnArmorEffectSO)
            spr = GameIconCatalog.GetActionIcon(ActionVisualId.StartNextTurnWithArmor)
                ?? GameIconCatalog.GetElementIcon(DieType.Armor);
        if (iconImage != null)
        {
            iconImage.sprite = spr;
            iconImage.enabled = spr != null;
            iconImage.raycastTarget = false;
        }

        var title = string.IsNullOrWhiteSpace(effect.Definition.effectName)
            ? effect.Definition.name
            : effect.Definition.effectName;
        ApplyTooltipContent(title, effect.Definition.description);
        UpdateStacks(effect.Stacks);
    }

    public void UpdateStacks(int stacks)
    {
        var hasStacks = stacks > 0 && stackText != null;
        if (stackText != null)
            stackText.text = hasStacks ? stacks.ToString() : string.Empty;
        if (stackVisualRoot != null)
            stackVisualRoot.SetActive(hasStacks);
    }

    void EnsureHoverTooltipTargets()
    {
        if (_hoverTargetsInitialized)
            return;
        _hoverTargetsInitialized = true;

        DisableDecorativeRaycasts();

        if (_hitAreaImage == null)
        {
            _hitAreaImage = GetComponent<Image>();
            if (_hitAreaImage == null)
                _hitAreaImage = gameObject.AddComponent<Image>();
            _hitAreaImage.color = new Color(1f, 1f, 1f, 0f);
            _hitAreaImage.raycastTarget = true;
        }

        hoverTooltipTarget = GetComponent<HoverTooltipTargetUI>() ?? gameObject.AddComponent<HoverTooltipTargetUI>();
        SyncHoverTooltipTarget();
    }

    /// <summary>Called by <see cref="StatusEffectBarUI"/> so player/enemy bars can pick above vs regular manager offset.</summary>
    public void ApplyBarTooltipPlacement(bool useHoverAboveScreenOffset)
    {
        showTooltipAbove = useHoverAboveScreenOffset;
        SyncHoverTooltipTarget();
    }

    void SyncHoverTooltipTarget()
    {
        if (hoverTooltipTarget == null)
            return;
        hoverTooltipTarget.SetTooltipScreenOffset(tooltipScreenOffset);
        hoverTooltipTarget.SetIsAbove(showTooltipAbove);
    }

    void DisableDecorativeRaycasts()
    {
        var rootTransform = transform;
        for (var i = 0; i < rootTransform.childCount; i++)
        {
            var child = rootTransform.GetChild(i);
            if (child == null)
                continue;

            var childImages = child.GetComponentsInChildren<Image>(true);
            for (var j = 0; j < childImages.Length; j++)
            {
                var image = childImages[j];
                if (image == null || image == _hitAreaImage)
                    continue;
                image.raycastTarget = false;
            }

            var childTexts = child.GetComponentsInChildren<TMP_Text>(true);
            for (var j = 0; j < childTexts.Length; j++)
            {
                if (childTexts[j] != null)
                    childTexts[j].raycastTarget = false;
            }
        }
    }

    void ApplyTooltipContent(string title, string description, Sprite tooltipBackground = null)
    {
        EnsureHoverTooltipTargets();

        if (hoverTooltipTarget != null)
            hoverTooltipTarget.SetContent(title, description, tooltipBackground);
    }
}
