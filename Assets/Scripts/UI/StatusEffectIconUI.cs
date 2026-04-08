using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;

    private void Awake()
    {
        if (iconImage == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': iconImage is not assigned!");
        if (stackText == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': stackText is not assigned!");
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
        if (iconImage != null)
        {
            iconImage.sprite = spr;
            iconImage.enabled = spr != null;
        }
        UpdateStacks(effect.Stacks);
    }

    public void UpdateStacks(int stacks)
    {
        stackText.text = stacks > 0 ? stacks.ToString() : "";
    }
}
