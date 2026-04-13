using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One relic icon in <see cref="RunRelicBarUI"/>; wire icon image, optional benefit badge.</summary>
public sealed class RunRelicSlotView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject benefitTextBackground;
    [SerializeField] private TMP_Text benefitText;

    public void Bind(RelicSO relic)
    {
        if (relic == null) return;

        if (iconImage != null)
        {
            iconImage.sprite = relic.icon;
            iconImage.enabled = relic.icon != null;
            iconImage.color = relic.icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
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
    }
}
