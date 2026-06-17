using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One relic icon in <see cref="RunRelicBarUI"/>; wire icon image, optional benefit badge.</summary>
public sealed class RunRelicSlotView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject benefitTextBackground;
    [SerializeField] private TMP_Text benefitText;
    [Tooltip("When true, adds extra upward screen offset so the shared hover tooltip reads above the slot.")]
    [SerializeField] private bool showTooltipAboveSlot = true;
    [Tooltip("Screen-space pixel offset passed to HoverTooltipManager (added to manager global offset).")]
    [SerializeField] private Vector2 tooltipScreenOffset = new Vector2(0f, 24f);

    HoverTooltipTargetUI _cachedHoverTarget;

    public void Bind(RelicSO relic)
    {
        if (relic == null)
            return;

        if (iconImage != null)
        {
            iconImage.sprite = relic.icon;
            iconImage.enabled = relic.icon != null;
            iconImage.color = relic.icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            iconImage.raycastTarget = true;
        }

        var showBenefit = relic.barBenefitDisplayValue != 0;
        if (benefitTextBackground != null)
            benefitTextBackground.SetActive(showBenefit);
        if (benefitText != null)
        {
            benefitText.gameObject.SetActive(showBenefit);
            if (showBenefit)
                benefitText.text = relic.barBenefitDisplayValue.ToString();
        }

        EnsureHoverTooltipForRelic(relic);
    }

    void EnsureHoverTooltipForRelic(RelicSO relic)
    {
        if (iconImage == null)
            return;

        StripLegacyRelicTooltips(gameObject);
        var go = iconImage.gameObject;
        StripLegacyRelicTooltips(go);

        if (_cachedHoverTarget == null || _cachedHoverTarget.gameObject != go)
            _cachedHoverTarget = go.GetComponent<HoverTooltipTargetUI>() ?? go.AddComponent<HoverTooltipTargetUI>();

        var offset = tooltipScreenOffset;
        if (showTooltipAboveSlot)
            offset.y += 24f;

        _cachedHoverTarget.SetTooltipScreenOffset(offset);
        _cachedHoverTarget.SetScriptableSource(relic);
    }

    static void StripLegacyRelicTooltips(GameObject target)
    {
        if (target == null)
            return;
        foreach (var legacy in target.GetComponents<RelicTooltipTrigger>())
        {
            if (legacy != null)
                Destroy(legacy);
        }
    }
}
