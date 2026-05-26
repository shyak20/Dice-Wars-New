using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Dice Select popup for a single completed trial. Invokes <see cref="OnCompleteClicked"/> when dismissed.</summary>
public sealed class ProgressionTrialCompletedPopupView : ProgressionCelebrationPopupViewBase
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image trialIconImage;
    [SerializeField] private Button completeButton;

    Action _onCompleteClicked;

    void Awake()
    {
        ResolvePanelRootInAwake();
        if (panelRoot == null)
            Debug.LogError($"ProgressionTrialCompletedPopupView on '{name}': assign panelRoot.", this);
        if (completeButton == null)
            Debug.LogError($"ProgressionTrialCompletedPopupView on '{name}': assign completeButton.", this);

        if (completeButton != null)
            completeButton.onClick.AddListener(HandleCompleteClicked);

        HideImmediate();
    }

    void OnDestroy()
    {
        if (completeButton != null)
            completeButton.onClick.RemoveListener(HandleCompleteClicked);
    }

    public void Show(PlayerTrialSO trial, Action onCompleteClicked)
    {
        if (trial == null)
        {
            Debug.LogError("ProgressionTrialCompletedPopupView.Show: trial is null.", this);
            return;
        }

        _onCompleteClicked = onCompleteClicked;

        if (titleText != null)
            titleText.text = ProgressionManager.BuildTrialCelebrationTitle(trial);

        if (bodyText != null)
            bodyText.text = BuildBody(trial);

        if (trialIconImage != null)
        {
            trialIconImage.sprite = trial.trialIcon;
            trialIconImage.enabled = trial.trialIcon != null;
        }

        ShowPanel();
    }

    public void Hide()
    {
        _onCompleteClicked = null;
        HideImmediate();
    }

    void HideImmediate() => HidePanelImmediate();

    void HandleCompleteClicked()
    {
        var callback = _onCompleteClicked;
        Hide();
        callback?.Invoke();
    }

    static string BuildBody(PlayerTrialSO trial)
    {
        var desc = trial.description ?? string.Empty;
        var rewardLine = ProgressionRewardDescriptionUtility.DescribeList(trial.completionRewards);
        if (string.IsNullOrWhiteSpace(rewardLine))
            return string.IsNullOrWhiteSpace(desc) ? "Trial completed." : desc;

        return string.IsNullOrWhiteSpace(desc)
            ? $"Rewards:\n{rewardLine}"
            : $"{desc}\n\nRewards:\n{rewardLine}";
    }
}
