using System;
using System.Collections.Generic;
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

    [Header("Rewards List Layout")]
    [Tooltip("Link the RectTransform under Body that has the VerticalLayoutGroup/ContentSizeFitter. Reward rows are instantiated as children. Optional.")]
    [SerializeField] private RectTransform rewardsContainer;

    [Header("Reward display")]
    [SerializeField] private ProgressionRewardVisualCatalogSO rewardVisualCatalog;

    [Header("Stat Row Prefab")]
    [Tooltip("Prefab used for stat reward rows. Must have CharacterInfoStat component.")]
    [SerializeField] private CharacterInfoStat statRowPrefab;

    [Header("Optional reward row prefabs")]
    [Tooltip("If set, used to render relic unlock rewards as icon + reward row text. If unset, the popup will try to clone an existing 'Relic Reward' instance in the scene.")]
    [SerializeField] private GameObject relicRewardDisplayPrefab;
    [Tooltip("If set, used to render gem unlock rewards as icon + reward row text. If unset, the relic reward prefab (or a scene instance) is used as fallback.")]
    [SerializeField] private GameObject gemRewardDisplayPrefab;
    [Tooltip("If set, used to render Add Starting Die rewards as icon + die name. Should contain a RankTrialRewardDisplay.")]
    [SerializeField] private GameObject addStartingDieRewardDisplayPrefab;
    [Tooltip("If set, used to render grouped face unlock rewards via RankTrialRewardDisplay. Falls back to relic reward prefab when unset.")]
    [SerializeField] private GameObject faceRewardDisplayPrefab;

    Action _onCompleteClicked;

    readonly List<GameObject> _spawnedRewardRows = new List<GameObject>();
    TMP_Text _descriptionLabel;
    RectTransform _rewardsContainerRt;
    bool _usingAutoCreatedRewardsContainer;

    void ClearSpawnedRows()
    {
        for (var i = 0; i < _spawnedRewardRows.Count; i++)
        {
            var go = _spawnedRewardRows[i];
            if (go != null)
                Destroy(go);
        }

        _spawnedRewardRows.Clear();
        ClearRewardsContainerChildren();
    }

    void ClearRewardsContainerChildren()
    {
        if (rewardsContainer == null)
            return;

        for (var i = rewardsContainer.childCount - 1; i >= 0; i--)
            Destroy(rewardsContainer.GetChild(i).gameObject);
    }

    ProgressionRewardVisualCatalogSO ResolveCatalog()
    {
        if (rewardVisualCatalog != null)
            return rewardVisualCatalog;

        var hover = FindObjectOfType<HoverTooltipManager>(true);
        return hover != null ? hover.ProgressionRewardVisualCatalog : null;
    }

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
            BuildBody(trial);

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
        ClearSpawnedRows();
        HideImmediate();
    }

    void HideImmediate() => HidePanelImmediate();

    void HandleCompleteClicked()
    {
        var callback = _onCompleteClicked;
        Hide();
        callback?.Invoke();
    }

    void BuildBody(PlayerTrialSO trial)
    {
        ClearSpawnedRows();

        var desc = string.IsNullOrWhiteSpace(trial.description) ? "Trial completed." : trial.description;
        SetupBodyLayout(desc);
        BuildRewardRows(trial.completionRewards, trial.completionRewardRowFormat);
    }

    void SetupBodyLayout(string description)
    {
        if (bodyText == null)
            return;

        var bodyRt = bodyText.rectTransform;

        var hlg = bodyRt.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
            hlg.enabled = false;

        var vlg = bodyRt.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
            vlg = bodyRt.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.spacing = 6f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = bodyRt.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = bodyRt.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var styleFont = bodyText.font;
        var styleFontSize = bodyText.fontSize;
        var styleColor = bodyText.color;
        var styleAlignment = bodyText.alignment;
        var styleWordWrap = bodyText.enableWordWrapping;

        bodyText.text = string.Empty;
        bodyText.enabled = false;

        if (_descriptionLabel == null)
        {
            var descGo = new GameObject("Popup Description", typeof(RectTransform));
            descGo.transform.SetParent(bodyRt.transform, false);

            _descriptionLabel = descGo.AddComponent<TextMeshProUGUI>();
            _descriptionLabel.raycastTarget = false;
        }

        _descriptionLabel.font = styleFont;
        _descriptionLabel.fontSize = styleFontSize;
        _descriptionLabel.color = styleColor;
        _descriptionLabel.alignment = styleAlignment;
        _descriptionLabel.enableWordWrapping = styleWordWrap;
        _descriptionLabel.text = description ?? string.Empty;

        _descriptionLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_descriptionLabel.text));

        var descRt = (RectTransform)_descriptionLabel.transform;
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 0f);
        descRt.pivot = new Vector2(0.5f, 0.5f);
        descRt.anchoredPosition = Vector2.zero;
        descRt.sizeDelta = new Vector2(0f, 0f);

        if (rewardsContainer != null)
        {
            _rewardsContainerRt = rewardsContainer;
            _usingAutoCreatedRewardsContainer = false;
        }
        else if (_rewardsContainerRt == null)
        {
            var rewardsGo = new GameObject("Popup Rewards", typeof(RectTransform));
            rewardsGo.transform.SetParent(bodyRt.transform, false);
            _rewardsContainerRt = rewardsGo.GetComponent<RectTransform>();
            _usingAutoCreatedRewardsContainer = true;
        }

        if (_usingAutoCreatedRewardsContainer)
        {
            var rewardsRt = _rewardsContainerRt;
            rewardsRt.anchorMin = new Vector2(0f, 0f);
            rewardsRt.anchorMax = new Vector2(1f, 0f);
            rewardsRt.pivot = new Vector2(0.5f, 0.0f);
            rewardsRt.anchoredPosition = Vector2.zero;
            rewardsRt.sizeDelta = new Vector2(0f, 0f);

            var rewardsVlg = rewardsRt.GetComponent<VerticalLayoutGroup>();
            if (rewardsVlg == null)
                rewardsVlg = rewardsRt.gameObject.AddComponent<VerticalLayoutGroup>();

            rewardsVlg.spacing = 6f;
            rewardsVlg.childAlignment = TextAnchor.UpperLeft;
            rewardsVlg.childControlWidth = true;
            rewardsVlg.childControlHeight = true;
            rewardsVlg.childForceExpandWidth = true;
            rewardsVlg.childForceExpandHeight = false;

            var rewardsFitter = rewardsRt.GetComponent<ContentSizeFitter>();
            if (rewardsFitter == null)
                rewardsFitter = rewardsRt.gameObject.AddComponent<ContentSizeFitter>();

            rewardsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rewardsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    void BuildRewardRows(IReadOnlyList<ProgressionRewardBase> rewards, string trialRowFormatOverride)
    {
        if (rewards == null || rewards.Count == 0 || bodyText == null)
            return;

        var catalog = ResolveCatalog();
        if (catalog == null)
        {
            Debug.LogError(
                $"ProgressionTrialCompletedPopupView on '{name}': assign rewardVisualCatalog or wire HoverTooltipManager.progressionRewardVisualCatalog.",
                this);
            return;
        }

        var entries = new List<ProgressionRewardDisplayEntry>();
        ProgressionRewardDisplayResolver.ExpandRewards(
            rewards,
            catalog,
            trialRowFormatOverride,
            ProgressionRewardExpandMode.Celebration,
            entries);

        var parent = (Transform)(_rewardsContainerRt != null ? _rewardsContainerRt : bodyText.rectTransform);
        ProgressionCelebrationRewardRowsUI.Populate(
            parent,
            entries,
            BuildRowPrefabs(),
            catalog,
            _spawnedRewardRows);
    }

    ProgressionCelebrationRewardRowsUI.Prefabs BuildRowPrefabs() => new ProgressionCelebrationRewardRowsUI.Prefabs
    {
        statRowPrefab = statRowPrefab,
        relicRewardDisplayPrefab = relicRewardDisplayPrefab,
        gemRewardDisplayPrefab = gemRewardDisplayPrefab,
        addStartingDieRewardDisplayPrefab = addStartingDieRewardDisplayPrefab,
        faceRewardDisplayPrefab = faceRewardDisplayPrefab != null
            ? faceRewardDisplayPrefab
            : relicRewardDisplayPrefab,
        textStyleTemplate = bodyText,
    };

    void OnDisable() => ClearSpawnedRows();
}
