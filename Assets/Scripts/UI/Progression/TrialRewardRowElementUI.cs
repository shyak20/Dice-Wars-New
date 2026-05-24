using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One reward row inside <see cref="HoverTrialRewardsTooltipPanelUI"/>.</summary>
public sealed class TrialRewardRowElementUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;

    public void Bind(ProgressionTrialRewardRowPresenter.RowViewModel row)
    {
        if (iconImage != null)
        {
            iconImage.sprite = row.Icon;
            iconImage.enabled = row.Icon != null;
        }

        if (labelText != null)
            labelText.text = row.Text;
    }
}
