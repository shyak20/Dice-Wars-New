using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Generic reward row for rank-up and trial completion celebration popups
/// (<see cref="ProgressionTrialCompletedPopupView"/>, <see cref="ProgressionRankUpPopupView"/>).
/// Supports relic unlocks, gem unlocks, and add-starting-die rewards.
///
/// Attach to the reward row prefab; the popup calls <see cref="BindRelic"/>, <see cref="BindGem"/>,
/// or <see cref="BindDie"/> after instantiating.
///
/// Fields:
///   - <see cref="iconImage"/> — relic / gem / die UI icon
///   - <see cref="nameText"/> — item display name
///   - <see cref="rewardTitleText"/> — line built from the reward Row Format (<c>string.Format</c> with <c>{0}</c>)
///   - <see cref="descriptionText"/> — relic/gem description (hidden when empty; dies have none)
///   - <see cref="hoverRoot"/> — optional object shown while hovering the row
/// </summary>
public sealed class RankTrialRewardDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [Tooltip("Icon for relic, gem, or die (DieAssetSO.uiIcon).")]
    [SerializeField] private Image iconImage;

    [Tooltip("Primary item name label.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("Formatted reward title from Row Format.")]
    [SerializeField] private TMP_Text rewardTitleText;

    [Tooltip("Relic/gem description (optional for die rows).")]
    [SerializeField] private TMP_Text descriptionText;

    [Header("Hover")]
    [Tooltip("Optional overlay toggled while the pointer hovers this row.")]
    [SerializeField] private GameObject hoverRoot;

    void Awake()
    {
        SetHoverVisible(false);
    }

    void OnDisable()
    {
        SetHoverVisible(false);
    }

    /// <summary>Populate for a relic reward.</summary>
    public void BindRelic(RelicSO relic, string rewardTitle)
    {
        if (relic == null)
        {
            Debug.LogError($"RankTrialRewardDisplay on '{name}': BindRelic called with null relic.", this);
            return;
        }

        var displayName = !string.IsNullOrWhiteSpace(relic.title) ? relic.title.Trim() : relic.name;
        Bind(relic.icon, displayName, rewardTitle, relic.description);
    }

    /// <summary>Populate for a gem reward.</summary>
    public void BindGem(GemSO gem, string rewardTitle)
    {
        if (gem == null)
        {
            Debug.LogError($"RankTrialRewardDisplay on '{name}': BindGem called with null gem.", this);
            return;
        }

        Bind(gem.icon, gem.DisplayLabel, rewardTitle, gem.description);
    }

    /// <summary>Populate for an add-starting-die reward (no description).</summary>
    public void BindDie(DieAssetSO die, string rewardTitle)
    {
        if (die == null)
        {
            Debug.LogError($"RankTrialRewardDisplay on '{name}': BindDie called with null die.", this);
            return;
        }

        var displayName = !string.IsNullOrWhiteSpace(die.dieName) ? die.dieName.Trim() : die.name;
        Bind(die.uiIcon, displayName, rewardTitle, string.Empty);
    }

    void Bind(Sprite icon, string displayName, string rewardTitle, string description)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
            nameText.text = displayName ?? string.Empty;

        if (rewardTitleText != null)
            rewardTitleText.text = rewardTitle ?? string.Empty;

        if (descriptionText != null)
        {
            descriptionText.text = description ?? string.Empty;
            descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
        }

        SetHoverVisible(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoverVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHoverVisible(false);
    }

    void SetHoverVisible(bool visible)
    {
        if (hoverRoot != null && hoverRoot.activeSelf != visible)
            hoverRoot.SetActive(visible);
    }
}
