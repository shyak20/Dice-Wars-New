using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;
    private HoverTooltipTargetUI hoverTooltipTarget;

    private void Awake()
    {
        if (iconImage == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': iconImage is not assigned!");
        if (stackText == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': stackText is not assigned!");

        var hoverTargetGo = iconImage != null ? iconImage.gameObject : gameObject;
        hoverTooltipTarget = hoverTargetGo.GetComponent<HoverTooltipTargetUI>() ?? hoverTargetGo.AddComponent<HoverTooltipTargetUI>();
    }

    public void Setup(StatusEffectInstance effect)
    {
        RefreshVisual(effect);
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
        UpdateStacks(effect.Stacks);
    }

    public void UpdateStacks(int stacks)
    {
        stackText.text = stacks > 0 ? stacks.ToString() : "";
    }
}
