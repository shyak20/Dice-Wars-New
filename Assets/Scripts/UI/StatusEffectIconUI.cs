using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;
    [Tooltip("Optional root for the stack badge visuals (text/background). Hidden when stacks are 0.")]
    [SerializeField] private GameObject stackVisualRoot;
    private HoverTooltipTargetUI hoverTooltipTarget;
    private HoverTooltipTargetUI hoverTooltipTargetOnIcon;

    private void Awake()
    {
        if (iconImage == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': iconImage is not assigned!");
        if (stackText == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': stackText is not assigned!");

        hoverTooltipTarget = GetComponent<HoverTooltipTargetUI>() ?? gameObject.AddComponent<HoverTooltipTargetUI>();
        if (iconImage != null)
        {
            hoverTooltipTargetOnIcon = iconImage.gameObject.GetComponent<HoverTooltipTargetUI>() ?? iconImage.gameObject.AddComponent<HoverTooltipTargetUI>();
            iconImage.raycastTarget = true;
        }
    }

    public void Setup(StatusEffectInstance effect)
    {
        RefreshVisual(effect);
    }

    public void SetupCustom(Sprite icon, string title, string description, Sprite tooltipBackground = null)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (hoverTooltipTarget != null)
            hoverTooltipTarget.SetContent(title, description, tooltipBackground);
        if (hoverTooltipTargetOnIcon != null)
            hoverTooltipTargetOnIcon.SetContent(title, description, tooltipBackground);

        UpdateStacks(0);
    }

    public void RefreshVisual(StatusEffectInstance effect)
    {
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
        if (iconImage != null)
        {
            iconImage.sprite = spr;
            iconImage.enabled = spr != null;
        }
        if (hoverTooltipTarget != null)
        {
            var title = string.IsNullOrWhiteSpace(effect.Definition.effectName) ? effect.Definition.name : effect.Definition.effectName;
            hoverTooltipTarget.SetContent(title, effect.Definition.description);
        }
        if (hoverTooltipTargetOnIcon != null)
        {
            var title = string.IsNullOrWhiteSpace(effect.Definition.effectName) ? effect.Definition.name : effect.Definition.effectName;
            hoverTooltipTargetOnIcon.SetContent(title, effect.Definition.description);
        }
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
}
