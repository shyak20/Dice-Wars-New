using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trial hover tooltip with title, description, and spawned <see cref="TrialRewardRowElementUI"/> rows.
/// Instantiated by <see cref="HoverTooltipManager"/>.
/// </summary>
public sealed class HoverTrialRewardsTooltipPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Transform rewardLayoutRoot;
    [SerializeField] private TrialRewardRowElementUI rewardRowPrefab;

    readonly List<TrialRewardRowElementUI> _spawnedRows = new List<TrialRewardRowElementUI>();

    void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
        EnsurePanelDoesNotBlockRaycasts();
        Hide();
    }

    public void Show(
        PlayerTrialSO trial,
        TrialSaveData state,
        GameIconIndexSO iconIndex,
        IReadOnlyList<ProgressionRewardBase> additionalRewards = null)
    {
        if (trial == null)
        {
            Hide();
            return;
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(trial.trialID)
                ? trial.name
                : trial.trialID.Trim();
        }

        if (descriptionText != null)
            descriptionText.text = ProgressionManager.BuildTrialTooltipBody(trial, state);

        RebuildRewardRows(trial, iconIndex, additionalRewards);

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    void RebuildRewardRows(
        PlayerTrialSO trial,
        GameIconIndexSO iconIndex,
        IReadOnlyList<ProgressionRewardBase> additionalRewards)
    {
        ClearRewardRows();

        if (rewardLayoutRoot == null || rewardRowPrefab == null)
            return;

        var rewards = new List<ProgressionRewardBase>();
        if (trial.completionReward != null)
            rewards.Add(trial.completionReward);
        if (additionalRewards != null)
        {
            for (var i = 0; i < additionalRewards.Count; i++)
            {
                var reward = additionalRewards[i];
                if (reward != null && reward != trial.completionReward)
                    rewards.Add(reward);
            }
        }

        var rows = new List<ProgressionTrialRewardRowPresenter.RowViewModel>();
        ProgressionTrialRewardRowPresenter.CollectRows(
            iconIndex,
            rewards,
            trial.completionReward,
            trial.completionRewardRowFormat,
            rows);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowView = Instantiate(rewardRowPrefab, rewardLayoutRoot);
            rowView.Bind(rows[i]);
            _spawnedRows.Add(rowView);
        }
    }

    public void AlignPivotWorldXToRect(RectTransform reference)
    {
        if (reference == null || panelRoot == null)
            return;

        var panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null)
            return;

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);
        var centerWorldX = (corners[0].x + corners[2].x) * 0.5f;
        var pos = panelRect.position;
        pos.x = centerWorldX;
        panelRect.position = pos;
    }

    public void AlignToRectWithScreenOffset(RectTransform reference, Vector2 screenOffset)
    {
        if (reference == null || panelRoot == null)
            return;

        var panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null)
            return;

        var parentRect = panelRect.parent as RectTransform;
        if (parentRect == null)
            return;

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);
        var centerWorld = (corners[0] + corners[2]) * 0.5f;

        var parentCanvas = GetComponentInParent<Canvas>();
        var cameraForCanvas = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;
        var screen = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, centerWorld) + screenOffset;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screen, cameraForCanvas, out var world))
            panelRect.position = world;
    }

    public void Hide()
    {
        if (titleText != null)
            titleText.text = string.Empty;
        if (descriptionText != null)
            descriptionText.text = string.Empty;

        ClearRewardRows();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void ClearRewardRows()
    {
        for (var i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i] != null)
                Destroy(_spawnedRows[i].gameObject);
        }

        _spawnedRows.Clear();
    }

    void EnsurePanelDoesNotBlockRaycasts()
    {
        if (panelRoot == null)
            return;

        var graphics = panelRoot.GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }
}
