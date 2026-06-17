using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One reward row inside <see cref="HoverTrialRewardsTooltipPanelUI"/> and celebration popups.</summary>
public sealed class TrialRewardRowElementUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;

    public void Bind(ProgressionRewardDisplayEntry entry)
    {
        if (iconImage != null)
        {
            iconImage.sprite = entry.icon;
            iconImage.enabled = entry.icon != null;
        }

        if (labelText != null)
            labelText.text = entry.text;
    }

    public void Bind(ProgressionTrialRewardRowPresenter.RowViewModel row) =>
        Bind(ProgressionRewardDisplayEntry.Compact(ProgressionRewardVisualKind.UnlockRelic, row.Icon, row.Text));
}
