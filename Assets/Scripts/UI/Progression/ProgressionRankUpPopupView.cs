using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Dice Select level-up popup shown after all trials on a rank are acknowledged.</summary>
public sealed class ProgressionRankUpPopupView : ProgressionCelebrationPopupViewBase
{
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Subtitle: character display name from PlayerDataSO. Hidden when unset or cleared.")]
    [SerializeField] private TMP_Text characterNameText;
    [Header("Rank portraits")]
    [Tooltip("Portrait for the rank being completed (e.g. Rank 0 when advancing to Rank 1).")]
    [SerializeField] private Image currentRankPortraitImage;
    [Tooltip("Portrait for the rank after level-up (e.g. Rank 1 when advancing from Rank 0).")]
    [SerializeField] private Image rankUpPortraitImage;
    [Header("Shown with popup")]
    [Tooltip("Turned on when this popup is shown; turned off when hidden or on startup.")]
    [SerializeField] private List<GameObject> objectsEnabledOnShow = new List<GameObject>();
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button completeButton;
    [Tooltip("Optional headline override. {0} = completed rank display name.")]
    [SerializeField] private string titleFormat = "Level Up — {0}";

    [Header("Rewards List Layout")]
    [Tooltip("Link the RectTransform under Body that has the VerticalLayoutGroup/ContentSizeFitter. Reward rows are instantiated as children. Optional.")]
    [SerializeField] private RectTransform rewardsContainer;

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
    }

    GameIconIndexSO ResolveGameIconIndex()
    {
        if (GameIconCatalog.Active != null)
            return GameIconCatalog.Active;

        // Dice Select scene doesn't register GameIconCatalog.Active; HoverTooltipManager has the serialized GameIconIndexSO reference.
        var hover = FindObjectOfType<HoverTooltipManager>(true);
        if (hover == null)
            return null;

        var f = typeof(HoverTooltipManager).GetField(
            "gameIconIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return f?.GetValue(hover) as GameIconIndexSO;
    }

    void Awake()
    {
        ResolvePanelRootInAwake();
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

        ApplyRankPortraitImage(currentRankPortraitImage, completedRank.Portrait);
        ApplyRankPortraitImage(rankUpPortraitImage, nextRank != null ? nextRank.Portrait : null);

        if (bodyText != null)
            BuildBody(completedRank);

        ShowPanel();
        SetObjectsEnabledOnShow(true);
    }

    public void Hide()
    {
        _onCompleteClicked = null;
        if (characterNameText != null)
        {
            characterNameText.text = string.Empty;
            characterNameText.gameObject.SetActive(false);
        }

        ApplyRankPortraitImage(currentRankPortraitImage, null);
        ApplyRankPortraitImage(rankUpPortraitImage, null);
        HideImmediate();
    }

    void HideImmediate()
    {
        SetObjectsEnabledOnShow(false);
        HidePanelImmediate();
    }

    void SetObjectsEnabledOnShow(bool active)
    {
        if (objectsEnabledOnShow == null)
            return;

        for (var i = 0; i < objectsEnabledOnShow.Count; i++)
        {
            var go = objectsEnabledOnShow[i];
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

        var iconIndex = ResolveGameIconIndex();

        SetupBodyLayout(desc);
        BuildRewardRows(rank.rankUpRewards, iconIndex);
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

        // Root TMP_Text is used only as a style/template for the new description label.
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

    void BuildRewardRows(IReadOnlyList<ProgressionRewardBase> rewards, GameIconIndexSO iconIndex)
    {
        if (rewards == null || rewards.Count == 0)
            return;

        var statTemplate = statRowPrefab != null
            ? statRowPrefab
            : FindObjectOfType<CharacterInfoStat>(true);

        for (var i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null)
                continue;

            switch (reward)
            {
                case ProgressionMaxHpReward hp:
                    AddStatRow(statTemplate, "Max Health", MainAttributeIconId.Hp, hp.amount, iconIndex);
                    break;
                case ProgressionMaxPowerReward power:
                    AddStatRow(statTemplate, "Base Max Power", MainAttributeIconId.Power, power.amount, iconIndex);
                    break;
                case ProgressionStartingGoldReward gold:
                    AddStatRow(statTemplate, "Starting Gold", MainAttributeIconId.Coins, gold.amount, iconIndex);
                    break;
                case ProgressionMapMoveLimitReward moves:
                    AddStatRow(statTemplate, "Map Moves", MainAttributeIconId.Movement, moves.amount, iconIndex);
                    break;
                case ProgressionMaxRollsReward maxRolls:
                    AddStatRow(statTemplate, "Max Rolls", MainAttributeIconId.ExtraRoll, maxRolls.amount, iconIndex);
                    break;
                case ProgressionExtraRollReward extraRoll:
                    AddStatRow(statTemplate, "Extra Rolls", MainAttributeIconId.ExtraRoll, extraRoll.amount, iconIndex);
                    break;

                case ProgressionUnlockRelicsReward unlockRelics:
                    AddRelicUnlockRows(unlockRelics, unlockRelics.relics);
                    break;
                case ProgressionUnlockGemsReward unlockGems:
                    AddGemUnlockRows(unlockGems, unlockGems.gems);
                    break;
                case ProgressionStartingRelicReward startingRelic:
                    AddRelicRow(startingRelic, startingRelic.relic);
                    break;
                case ProgressionAddStartingDieReward addDie:
                    AddDieRow(addDie, addDie.die);
                    break;

                default:
                    AddTextRow(ProgressionRewardDescriptionUtility.Describe(reward));
                    break;
            }
        }
    }

    void AddStatRow(CharacterInfoStat statTemplate, string label, MainAttributeIconId iconId, int amount, GameIconIndexSO iconIndex)
    {
        if (statTemplate == null || bodyText == null)
            return;

        var parent = (Transform)(_rewardsContainerRt != null ? _rewardsContainerRt : bodyText.rectTransform);
        var go = Instantiate(statTemplate.gameObject, parent, false);
        var stat = go.GetComponent<CharacterInfoStat>();
        if (stat == null)
        {
            Destroy(go);
            return;
        }

        stat.SetLabel(label);
        stat.SetMainAttributeIconId(iconId);
        stat.SetValue($"+{amount}", iconIndex);

        _spawnedRewardRows.Add(go);
    }

    void AddRelicUnlockRows(ProgressionRewardBase reward, List<RelicSO> relics)
    {
        if (relics == null)
            return;

        for (var i = 0; i < relics.Count; i++)
            AddRelicRow(reward, relics[i]);
    }

    void AddGemUnlockRows(ProgressionRewardBase reward, List<GemSO> gems)
    {
        if (gems == null)
            return;

        for (var i = 0; i < gems.Count; i++)
            AddGemRow(reward, gems[i]);
    }

    void AddRelicRow(ProgressionRewardBase reward, RelicSO relic)
    {
        if (relic == null)
            return;

        var displayName = !string.IsNullOrWhiteSpace(relic.title) ? relic.title.Trim() : relic.name;
        var rewardTitle = ProgressionTrialRewardRowPresenter.FormatRelicGemRowTitle(reward, displayName);

        var go = InstantiateRelicGemRow(relicRewardDisplayPrefab, "Relic Reward", "Relic Unlock Row");
        if (go == null)
        {
            AddTextRow(rewardTitle);
            return;
        }

        var view = go.GetComponent<RankTrialRewardDisplay>();
        if (view != null)
            view.BindRelic(relic, rewardTitle);
        else
            TryBindLegacyRelicOrGemRow(go, relic.icon, rewardTitle, false);

        _spawnedRewardRows.Add(go);
    }

    void AddGemRow(ProgressionRewardBase reward, GemSO gem)
    {
        if (gem == null)
            return;

        var rewardTitle = ProgressionTrialRewardRowPresenter.FormatRelicGemRowTitle(reward, gem.DisplayLabel);

        var go = InstantiateRelicGemRow(gemRewardDisplayPrefab, "Gem Reward", "Gem Unlock Row");
        if (go == null)
        {
            AddTextRow(rewardTitle);
            return;
        }

        var view = go.GetComponent<RankTrialRewardDisplay>();
        if (view != null)
            view.BindGem(gem, rewardTitle);
        else
            TryBindLegacyRelicOrGemRow(go, gem.icon, rewardTitle, true);

        _spawnedRewardRows.Add(go);
    }

    void AddDieRow(ProgressionRewardBase reward, DieAssetSO die)
    {
        if (die == null)
            return;

        var displayName = !string.IsNullOrWhiteSpace(die.dieName) ? die.dieName.Trim() : die.name;
        var rewardTitle = ProgressionTrialRewardRowPresenter.FormatRelicGemRowTitle(reward, displayName);

        var go = InstantiateRelicGemRow(addStartingDieRewardDisplayPrefab, null, "Add Starting Die Row");
        if (go == null)
        {
            AddTextRow(rewardTitle);
            return;
        }

        var view = go.GetComponent<RankTrialRewardDisplay>();
        if (view != null)
            view.BindDie(die, rewardTitle);
        else
            TryBindLegacyRelicOrGemRow(go, die.uiIcon, rewardTitle, false);

        _spawnedRewardRows.Add(go);
    }

    GameObject InstantiateRelicGemRow(GameObject prefab, string sceneFallbackName, string newName)
    {
        var template = prefab;
        if (template == null && !string.IsNullOrEmpty(sceneFallbackName))
        {
            // Best-effort fallback: clone an existing instance already present in the current scene.
            template = GameObject.Find(sceneFallbackName);
        }

        if (template == null || bodyText == null)
            return null;

        var parent = (Transform)(_rewardsContainerRt != null ? _rewardsContainerRt : bodyText.rectTransform);
        var go = Instantiate(template, parent, false);
        go.name = newName;
        go.SetActive(true);
        return go;
    }

    void TryBindLegacyRelicOrGemRow(GameObject rowGo, Sprite iconSprite, string text, bool isGem)
    {
        if (rowGo == null)
            return;

        var tmpAll = rowGo.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text target = null;
        var desired = isGem ? "Die Gem" : "Artifact";
        if (tmpAll != null)
        {
            for (var i = 0; i < tmpAll.Length; i++)
            {
                var t = tmpAll[i];
                if (t == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(t.text) && t.text.IndexOf(desired, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    target = t;
                    break;
                }
            }
        }

        if (target == null && tmpAll != null && tmpAll.Length > 0)
            target = tmpAll[0];

        if (target != null)
            target.text = text ?? string.Empty;

        var images = rowGo.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
            return;

        Image icon = null;
        for (var i = 0; i < images.Length; i++)
        {
            var img = images[i];
            var n = img != null ? img.gameObject.name : string.Empty;
            if (!string.IsNullOrWhiteSpace(n) &&
                (n.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 n.IndexOf("Relic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 n.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                icon = img;
                break;
            }
        }

        if (icon == null && images.Length > 1)
            icon = images[1];
        if (icon == null)
            icon = images[0];

        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
    }

    void AddTextRow(string text)
    {
        if (bodyText == null)
            return;

        var go = new GameObject("Reward Text Row", typeof(RectTransform));
        var parent = (Transform)(_rewardsContainerRt != null ? _rewardsContainerRt : bodyText.rectTransform);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.font = bodyText.font;
        tmp.fontSize = bodyText.fontSize;
        tmp.color = bodyText.color;
        tmp.alignment = bodyText.alignment;
        tmp.enableWordWrapping = bodyText.enableWordWrapping;
        tmp.text = text ?? string.Empty;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);

        _spawnedRewardRows.Add(go);
    }

    void OnDisable() => ClearSpawnedRows();

    static void ApplyRankPortraitImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}
