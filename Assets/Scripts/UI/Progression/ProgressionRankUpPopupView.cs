using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Dice Select level-up popup shown after all trials on a rank are acknowledged.</summary>
public sealed class ProgressionRankUpPopupView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button completeButton;
    [Tooltip("Optional headline override. {0} = completed rank display name.")]
    [SerializeField] private string titleFormat = "Level Up — {0}";

    Action _onCompleteClicked;

    void Awake()
    {
        if (panelRoot == null)
            Debug.LogError($"ProgressionRankUpPopupView on '{name}': assign panelRoot.", this);
        if (completeButton == null)
            Debug.LogError($"ProgressionRankUpPopupView on '{name}': assign completeButton.", this);

        if (completeButton != null)
            completeButton.onClick.AddListener(HandleCompleteClicked);

        HideImmediate();
    }

    void OnDestroy()
    {
        if (completeButton != null)
            completeButton.onClick.RemoveListener(HandleCompleteClicked);
    }

    public void Show(PlayerRankSO completedRank, Action onCompleteClicked)
    {
        if (completedRank == null)
        {
            Debug.LogError("ProgressionRankUpPopupView.Show: completedRank is null.", this);
            return;
        }

        _onCompleteClicked = onCompleteClicked;

        var rankLabel = string.IsNullOrWhiteSpace(completedRank.rankName)
            ? $"Rank {completedRank.rankIndex}"
            : completedRank.rankName;

        if (titleText != null)
        {
            var format = string.IsNullOrWhiteSpace(titleFormat) ? "{0}" : titleFormat;
            try
            {
                titleText.text = string.Format(format, rankLabel);
            }
            catch (FormatException)
            {
                titleText.text = $"Level Up — {rankLabel}";
            }
        }

        if (bodyText != null)
            bodyText.text = BuildBody(completedRank);

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Hide()
    {
        _onCompleteClicked = null;
        HideImmediate();
    }

    void HideImmediate()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void HandleCompleteClicked()
    {
        var callback = _onCompleteClicked;
        Hide();
        callback?.Invoke();
    }

    static string BuildBody(PlayerRankSO rank)
    {
        var flavor = rank.rankFlavorText ?? string.Empty;
        var rewards = ProgressionRewardDescriptionUtility.DescribeList(rank.rankUpRewards);
        if (string.IsNullOrWhiteSpace(rewards))
            return string.IsNullOrWhiteSpace(flavor) ? "All trials complete. Rank increased!" : flavor;

        return string.IsNullOrWhiteSpace(flavor)
            ? $"Rewards:\n{rewards}"
            : $"{flavor}\n\nRewards:\n{rewards}";
    }
}
