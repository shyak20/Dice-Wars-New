using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Dice Select level-up popup shown after all trials on a rank are acknowledged.</summary>
public sealed class ProgressionRankUpPopupView : ProgressionCelebrationPopupViewBase
{
    const float CurrentRankInitialReveal = 1f;
    const float RankUpInitialReveal = 0f;

    [SerializeField] private TMP_Text titleText;
    [Tooltip("Subtitle: character display name from PlayerDataSO. Hidden when unset or cleared.")]
    [SerializeField] private TMP_Text characterNameText;
    [Header("Rank portraits")]
    [Tooltip("Portrait for the rank being completed (e.g. Rank 0 when advancing to Rank 1).")]
    [SerializeField] private RankPortraitPrefabHost currentRankPortraitHost;
    [Tooltip("Portrait for the rank after level-up (e.g. Rank 1 when advancing from Rank 0).")]
    [SerializeField] private RankPortraitPrefabHost rankUpPortraitHost;
    [Header("Spawned portrait sprites")]
    [Tooltip("Order in Layer for the completed-rank portrait instantiated during level-up.")]
    [SerializeField] private int currentRankSpawnedSortingOrder;
    [Tooltip("Order in Layer for the next-rank portrait instantiated during level-up.")]
    [SerializeField] private int rankUpSpawnedSortingOrder = 4;
    [Tooltip("Optional sorting layer for both spawned portraits. Leave empty to keep each renderer's layer.")]
    [SerializeField] private string spawnedPortraitSortingLayerName;
    [SerializeField] private Material portraitSplatterMaterial;
    [Tooltip("Optional: hidden while this popup is visible so it does not overlap the celebration portraits.")]
    [SerializeField] private DiceSelectLargePortraitPresenter diceSelectLargePortraitPresenter;
    [Header("Rank up scene toggles")]
    [Tooltip("Activated when the rank-up popup is shown; deactivated when it is hidden.")]
    [FormerlySerializedAs("objectsEnabledOnShow")]
    [SerializeField] private List<GameObject> objectsToEnableOnRankUp = new List<GameObject>();
    [Tooltip("Deactivated when the rank-up popup is shown; only entries that were active are restored when hidden.")]
    [SerializeField] private List<GameObject> objectsToDisableOnRankUp = new List<GameObject>();
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button completeButton;
    [Tooltip("Optional headline override. {0} = completed rank display name.")]
    [SerializeField] private string titleFormat = "Level Up — {0}";

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
    [Tooltip("Compact icon + label row for grouped face unlocks (same prefab as trial hover tooltip).")]
    [SerializeField] private TrialRewardRowElementUI compactRewardRowPrefab;

    Action _onCompleteClicked;
    bool _hidDiceSelectPortrait;

    readonly List<GameObject> _spawnedRewardRows = new List<GameObject>();
    readonly List<GameObject> _disabledForRankUp = new List<GameObject>();
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
            Debug.LogError($"ProgressionRankUpPopupView on '{name}': assign panelRoot.", this);
        if (completeButton == null)
            Debug.LogError($"ProgressionRankUpPopupView on '{name}': assign completeButton.", this);
        if (currentRankPortraitHost == null)
            Debug.LogError($"ProgressionRankUpPopupView on '{name}': assign currentRankPortraitHost.", this);
        if (rankUpPortraitHost == null)
            Debug.LogError($"ProgressionRankUpPopupView on '{name}': assign rankUpPortraitHost.", this);
        if (portraitSplatterMaterial == null)
            Debug.LogError($"ProgressionRankUpPopupView on '{name}': assign portraitSplatterMaterial.", this);

        if (completeButton != null)
            completeButton.onClick.AddListener(HandleCompleteClicked);

        HideImmediate();
    }

    void OnDestroy()
    {
        if (completeButton != null)
            completeButton.onClick.RemoveListener(HandleCompleteClicked);
    }

    public void Show(PlayerRankSO completedRank, Action onCompleteClicked) =>
        Show(completedRank, null, onCompleteClicked);

    /// <param name="character">Preview character whose display name appears under the title when <see cref="characterNameText"/> is assigned.</param>
    public void Show(PlayerRankSO completedRank, PlayerDataSO character, Action onCompleteClicked)
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

        if (characterNameText != null)
        {
            var name = character != null ? character.DisplayName : string.Empty;
            characterNameText.text = name ?? string.Empty;
            characterNameText.gameObject.SetActive(!string.IsNullOrWhiteSpace(name));
        }

        PlayerRankSO nextRank = null;
        if (character != null)
            ProgressionRankPortraitUtility.TryGetNextRank(character, completedRank, out nextRank);

        HideDiceSelectPortraitIfConfigured();
        ApplyRankPortraits(completedRank, nextRank);

        if (bodyText != null)
            BuildBody(completedRank);

        ShowPanel();
        ApplyRankUpSceneToggles(rankUpActive: true);
    }

    public void Hide()
    {
        _onCompleteClicked = null;
        if (characterNameText != null)
        {
            characterNameText.text = string.Empty;
            characterNameText.gameObject.SetActive(false);
        }

        ClearRankPortraits();
        RestoreDiceSelectPortraitIfHidden();
        HideImmediate();
    }

    void HideImmediate()
    {
        ApplyRankUpSceneToggles(rankUpActive: false);
        HidePanelImmediate();
    }

    void ApplyRankPortraits(PlayerRankSO completedRank, PlayerRankSO nextRank)
    {
        currentRankPortraitHost?.Clear();
        rankUpPortraitHost?.Clear();

        if (currentRankPortraitHost != null && completedRank != null)
        {
            currentRankPortraitHost.ConfigureSpawnedSorting(
                currentRankSpawnedSortingOrder,
                spawnedPortraitSortingLayerName);
            if (currentRankPortraitHost.ApplyRank(completedRank))
            {
                currentRankPortraitHost.PrepareManualReveal(portraitSplatterMaterial);
                currentRankPortraitHost.SetManualRevealImmediate(CurrentRankInitialReveal);
            }
        }

        if (rankUpPortraitHost != null && nextRank != null)
        {
            rankUpPortraitHost.ConfigureSpawnedSorting(
                rankUpSpawnedSortingOrder,
                spawnedPortraitSortingLayerName);
            if (rankUpPortraitHost.ApplyRank(nextRank))
            {
                rankUpPortraitHost.PrepareManualReveal(portraitSplatterMaterial);
                rankUpPortraitHost.SetManualRevealImmediate(RankUpInitialReveal);
            }
        }
    }

    void ClearRankPortraits()
    {
        currentRankPortraitHost?.Clear();
        rankUpPortraitHost?.Clear();
    }

    void HideDiceSelectPortraitIfConfigured()
    {
        _hidDiceSelectPortrait = false;
        if (diceSelectLargePortraitPresenter == null)
            return;

        diceSelectLargePortraitPresenter.SetSpawnRootActive(false);
        _hidDiceSelectPortrait = true;
    }

    void RestoreDiceSelectPortraitIfHidden()
    {
        if (!_hidDiceSelectPortrait || diceSelectLargePortraitPresenter == null)
            return;

        diceSelectLargePortraitPresenter.SetSpawnRootActive(true);
        _hidDiceSelectPortrait = false;
    }

    void ApplyRankUpSceneToggles(bool rankUpActive)
    {
        if (rankUpActive)
        {
            SetListActive(objectsToEnableOnRankUp, true);

            _disabledForRankUp.Clear();
            if (objectsToDisableOnRankUp == null)
                return;

            for (var i = 0; i < objectsToDisableOnRankUp.Count; i++)
            {
                var go = objectsToDisableOnRankUp[i];
                if (go == null || !go.activeSelf)
                    continue;

                _disabledForRankUp.Add(go);
                go.SetActive(false);
            }

            return;
        }

        SetListActive(objectsToEnableOnRankUp, false);

        for (var i = 0; i < _disabledForRankUp.Count; i++)
        {
            var go = _disabledForRankUp[i];
            if (go != null)
                go.SetActive(true);
        }

        _disabledForRankUp.Clear();
    }

    static void SetListActive(IReadOnlyList<GameObject> objects, bool active)
    {
        if (objects == null)
            return;

        for (var i = 0; i < objects.Count; i++)
        {
            var go = objects[i];
            if (go != null)
                go.SetActive(active);
        }
    }

    void HandleCompleteClicked()
    {
        var callback = _onCompleteClicked;
        Hide();
        callback?.Invoke();
    }

    void BuildBody(PlayerRankSO rank)
    {
        ClearSpawnedRows();

        var desc = string.IsNullOrWhiteSpace(rank.rankFlavorText)
            ? "All trials complete. Rank increased!"
            : rank.rankFlavorText;

        SetupBodyLayout(desc);
        BuildRewardRows(rank.rankUpRewards);
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

    void BuildRewardRows(IReadOnlyList<ProgressionRewardBase> rewards)
    {
        if (rewards == null || rewards.Count == 0 || bodyText == null)
            return;

        var catalog = ResolveCatalog();
        if (catalog == null)
        {
            Debug.LogError(
                $"ProgressionRankUpPopupView on '{name}': assign rewardVisualCatalog or wire HoverTooltipManager.progressionRewardVisualCatalog.",
                this);
            return;
        }

        var entries = new List<ProgressionRewardDisplayEntry>();
        ProgressionRewardDisplayResolver.ExpandRewards(
            rewards,
            catalog,
            trialRowFormatOverride: null,
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
        compactRowPrefab = compactRewardRowPrefab,
        textStyleTemplate = bodyText,
    };

    void OnDisable() => ClearSpawnedRows();
}
