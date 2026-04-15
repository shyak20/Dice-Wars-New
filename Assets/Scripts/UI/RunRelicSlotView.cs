using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One relic icon in <see cref="RunRelicBarUI"/>; wire icon image, optional benefit badge.</summary>
public sealed class RunRelicSlotView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject benefitTextBackground;
    [SerializeField] private TMP_Text benefitText;
    [Tooltip("Optional; add RelicTooltipTrigger on this object or the icon Image. Hover shows RelicTooltipUI.")]
    [SerializeField] private RelicTooltipTrigger relicTooltipTrigger;

    public void Bind(RelicSO relic)
    {
        if (relic == null) return;

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

        if (relicTooltipTrigger != null)
            relicTooltipTrigger.SetRelic(relic);
    }
}
