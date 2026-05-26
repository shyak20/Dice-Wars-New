using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-character rank/trial observer. Subscribes to <see cref="ProgressionEvents"/> for active trial types only.
/// </summary>
public sealed class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance { get; private set; }

    [Tooltip("Fallback when PlayerDataSO.progressionCatalog is not assigned.")]
    [SerializeField] private ProgressionCatalogSO defaultCatalog;

    static ProgressionCatalogSO _pendingBootstrapCatalog;

    PlayerDataSO _activeTemplate;
    ProgressionCatalogSO _activeCatalog;
    ProgressionProfileSaveData _save;
    PlayerRankSO _activeRank;
    readonly HashSet<TrialType> _subscribedTrialTypes = new HashSet<TrialType>();
    readonly Dictionary<string, TrialSaveData> _trialStateById = new Dictionary<string, TrialSaveData>(StringComparer.Ordinal);
    readonly Dictionary<string, int> _lifetimeAccumulated = new Dictionary<string, int>(StringComparer.Ordinal);

    public ProgressionCatalogSO Catalog => _activeCatalog != null ? _activeCatalog : defaultCatalog;

    public PlayerDataSO ActiveCharacterTemplate => _activeTemplate;

    public event Action<PlayerTrialSO> OnTrialCompleted;
    public event Action<PlayerRankSO> OnRankUp;
    public event Action<PlayerDataSO> OnProgressionChanged;

    public static event Action<PlayerDataSO> OnCharacterProgressionChanged;

    public static ProgressionManager TryGetRuntime()
    {
        if (Instance != null)
            return Instance;
        return FindObjectOfType<ProgressionManager>(true);
    }

    public static ProgressionManager EnsureRuntime(ProgressionCatalogSO bootstrapCatalog)
    {
        var existing = TryGetRuntime();
        if (existing != null)
            return existing;

        if (bootstrapCatalog == null)
            return null;

        Debug.LogWarning(
            "ProgressionManager: No manager in scene — creating runtime fallback. " +
            "Add ProgressionManager next to RunManager in your bootstrap scene.");

        _pendingBootstrapCatalog = bootstrapCatalog;
        var go = new GameObject(nameof(ProgressionManager));
        var created = go.AddComponent<ProgressionManager>();
        _pendingBootstrapCatalog = null;
        return created;
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        if (defaultCatalog == null && _pendingBootstrapCatalog != null)
            defaultCatalog = _pendingBootstrapCatalog;

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (defaultCatalog == null)
            Debug.LogError("ProgressionManager: assign defaultCatalog (or set progressionCatalog on each PlayerDataSO).", this);
    }

    void OnDestroy()
    {
        FlushProgressToDisk();
        UnsubscribeAll();
        if (Instance == this)
            Instance = null;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            FlushProgressToDisk();
    }

    void OnApplicationQuit() => FlushProgressToDisk();

    void FlushProgressToDisk()
    {
        if (_activeTemplate == null || _save == null)
            return;

        Persist();
    }

    /// <summary>True when <paramref name="template"/> is already loaded and trials are tracking.</summary>
    public bool IsInitializedFor(PlayerDataSO template) =>
        template != null && _activeTemplate == template && _save != null && _activeCatalog != null;

    /// <summary>Loads progression when the active character changes. No-op if already bound (avoids UI refresh loops).</summary>
    public void SwitchToCharacter(PlayerDataSO template)
    {
        if (template == null)
        {
            Debug.LogError("ProgressionManager.SwitchToCharacter: template is null.");
            return;
        }

        if (IsInitializedFor(template))
            return;

        InitializeForCharacter(template);
    }

    /// <summary>Reloads saved progress for the current character and notifies listeners (e.g. after a run ends).</summary>
    public void RefreshCharacterProgression(PlayerDataSO template)
    {
        if (template == null)
        {
            Debug.LogError("ProgressionManager.RefreshCharacterProgression: template is null.");
            return;
        }

        if (!IsInitializedFor(template))
        {
            InitializeForCharacter(template);
            return;
        }

        ReloadProgressFromStorage();
        SyncActiveRankAndTrials();
        RebuildEventSubscriptions();
        BackfillUnappliedRankUpRewards();
        SyncGrantedStartingDiceFromSave();
        Persist();
        NotifyChanged();
    }

    public void InitializeForCharacter(PlayerDataSO template)
    {
        if (template == null)
        {
            Debug.LogError("ProgressionManager.InitializeForCharacter: template is null.");
            return;
        }

        _activeCatalog = ResolveCatalogForCharacter(template);
        if (_activeCatalog == null)
        {
            Debug.LogError(
                $"ProgressionManager.InitializeForCharacter: no catalog for '{template.DisplayName}'. " +
                "Assign progressionCatalog on the PlayerDataSO asset.", template);
            return;
        }

        if (!_activeCatalog.ValidateUniqueTrialIds())
        {
            Debug.LogError(
                $"ProgressionManager.InitializeForCharacter: fix duplicate or empty trialID values on catalog '{_activeCatalog.name}'.",
                _activeCatalog);
            return;
        }

        _activeTemplate = template;
        _save = ProgressionSaveService.Load(template.MetaSaveId);
        ClampRankIndex();
        RebuildLifetimeCacheFromSave();
        AbsorbSavedTrialStatesIntoLifetime();
        var migratedLegacySave = MigrateLifetimeCountersFromLegacySave();
        SyncActiveRankAndTrials();
        RebuildEventSubscriptions();
        BackfillUnappliedRankUpRewards();
        SyncGrantedStartingDiceFromSave();
        if (migratedLegacySave)
            Persist();
        NotifyChanged();
    }

    public IReadOnlyList<TrialSaveData> GetActiveTrialStatesForUI()
    {
        var list = new List<TrialSaveData>();
        foreach (var kv in _trialStateById)
            list.Add(kv.Value);
        return list;
    }

    public bool TryGetTrialState(string trialId, out TrialSaveData state)
    {
        if (string.IsNullOrEmpty(trialId))
        {
            state = default;
            return false;
        }

        return _trialStateById.TryGetValue(trialId, out state);
    }

    public void GetActiveRankTrialCounts(out int completed, out int total)
    {
        completed = 0;
        total = 0;
        if (_activeRank?.associatedTrials == null)
            return;

        for (var i = 0; i < _activeRank.associatedTrials.Count; i++)
        {
            var trial = _activeRank.associatedTrials[i];
            if (trial == null || ProgressionContentIds.IsNullOrEmpty(trial.TrialId))
                continue;

            total++;
            if (_trialStateById.TryGetValue(trial.TrialId, out var state) && state.isCompleted)
                completed++;
        }
    }

    public bool HasPendingCelebrations()
    {
        if (_save == null)
            return false;

        return HasUnacknowledgedTrialCelebrations() || _save.pendingRankUpCelebration;
    }

    public bool HasUnacknowledgedTrialCelebrations() =>
        _save?.unacknowledgedTrialIds != null && _save.unacknowledgedTrialIds.Count > 0;

    public bool HasPendingRankUpCelebration() =>
        _save != null && _save.pendingRankUpCelebration;

    /// <summary>Pending trial completions for the active rank, in catalog order.</summary>
    public void CollectUnacknowledgedTrials(List<PlayerTrialSO> into)
    {
        into.Clear();
        if (_save?.unacknowledgedTrialIds == null || _activeRank?.associatedTrials == null)
            return;

        for (var i = 0; i < _activeRank.associatedTrials.Count; i++)
        {
            var trial = _activeRank.associatedTrials[i];
            if (trial == null || ProgressionContentIds.IsNullOrEmpty(trial.TrialId))
                continue;

            if (IsTrialUnacknowledged(trial.TrialId))
                into.Add(trial);
        }
    }

    public void AcknowledgeTrialCelebration(string trialId)
    {
        if (_save?.unacknowledgedTrialIds == null || ProgressionContentIds.IsNullOrEmpty(trialId))
            return;

        _save.unacknowledgedTrialIds.RemoveAll(id => string.Equals(id, trialId, StringComparison.Ordinal));
        Persist();
        NotifyChanged();
    }

    /// <summary>Applies rank-up rewards and advances rank after the Dice Select level-up popup is dismissed.</summary>
    public void AcknowledgeRankUpCelebration()
    {
        if (_save == null || !_save.pendingRankUpCelebration)
            return;

        _save.pendingRankUpCelebration = false;
        TryAdvanceRank();
    }

    public static string BuildTrialCelebrationTitle(PlayerTrialSO trial)
    {
        if (trial == null)
            return "Trial Complete";

        return string.IsNullOrWhiteSpace(trial.DisplayName)
            ? "Trial Complete"
            : $"Trial Complete: {trial.DisplayName}";
    }

    public static string BuildTrialTooltipBody(PlayerTrialSO trial, TrialSaveData state)
    {
        if (trial == null)
            return string.Empty;

        var desc = trial.description ?? string.Empty;
        if (state.isCompleted)
            return string.IsNullOrEmpty(desc) ? "Completed." : desc + "\n\nCompleted.";

        var progress = Mathf.Max(0, state.currentValue);
        var progressLine = $"Progress: {progress}/{trial.targetValue}";
        return string.IsNullOrEmpty(desc) ? progressLine : desc + "\n\n" + progressLine;
    }

    public int GetCurrentRankIndex() => _save?.currentRankIndex ?? 0;

    public PlayerRankSO GetActiveRank() => _activeRank;

    public int GetStartingHPModifier() =>
        ProgressionRunModifiers.SumMaxHp(Catalog, _save);

    public int GetStartingGoldModifier() =>
        ProgressionRunModifiers.SumStartingGold(Catalog, _save);

    public int GetMaxPowerModifier() =>
        ProgressionRunModifiers.SumMaxPower(Catalog, _save);

    public int GetGridMoveModifier() =>
        ProgressionRunModifiers.SumMapMoveLimit(Catalog, _save);

    public int GetMaxRollsModifier() =>
        ProgressionRunModifiers.SumExtraRolls(Catalog, _save);

    public int GetExtraRollModifier() => GetMaxRollsModifier();

    public bool IsContentUnlocked(string contentId) =>
        ProgressionRunModifiers.IsContentUnlocked(Catalog, _save, contentId);

    public bool HasHorizontalUnlockGates() =>
        ProgressionRunModifiers.HasHorizontalUnlocks(Catalog);

    public void ApplyToRuntimeProfile(PlayerDataSO runtimeProfile, PlayerDataSO template)
    {
        if (runtimeProfile == null || template == null)
        {
            Debug.LogError("ProgressionManager.ApplyToRuntimeProfile: null profile.");
            return;
        }

        if (_activeTemplate != template || _save == null)
            InitializeForCharacter(template);

        runtimeProfile.startingMaxHealth = template.startingMaxHealth + GetStartingHPModifier();
        runtimeProfile.baseMaxPower = template.baseMaxPower + GetMaxPowerModifier();
        runtimeProfile.moveLimit = template.moveLimit + GetGridMoveModifier();
        runtimeProfile.maxRollsPerTurn = template.maxRollsPerTurn + GetMaxRollsModifier();

    }

    public List<DieAssetSO> BuildEffectiveStartingDeckTemplates(PlayerDataSO template)
    {
        if (template == null)
            return new List<DieAssetSO>();

        var catalog = ResolveCatalogForCharacter(template);
        if (catalog == null)
            return ProgressionStartingDiceUtility.CopyDeckReferences(template.currentDeck);

        if (IsInitializedFor(template))
            return ProgressionStartingDiceUtility.BuildEffectiveDeck(Catalog, _save, template);

        var save = ProgressionSaveService.Load(template.MetaSaveId);
        ProgressionStartingDiceUtility.StripLegacyGrantsFromTemplate(catalog, save, template);
        return ProgressionStartingDiceUtility.BuildEffectiveDeck(catalog, save, template);
    }

    public int GetStartingGoldForNewRun() => GetStartingGoldModifier();

    /// <summary>Relics equipped when a new run begins (rank-up / trial <see cref="ProgressionStartingRelicReward"/>).</summary>
    public IReadOnlyList<RelicSO> GetStartingRelicsForNewRun()
    {
        var relics = new List<RelicSO>();
        ProgressionRunModifiers.CollectStartingRelics(Catalog, _save, relics);
        return relics;
    }

    void SyncGrantedStartingDiceFromSave()
    {
        if (_activeTemplate == null || _save == null || Catalog == null)
            return;

        ProgressionStartingDiceUtility.StripLegacyGrantsFromTemplate(Catalog, _save, _activeTemplate);
        ProgressionStartingDiceUtility.UpgradeGrantEntryDieTypes(Catalog, _save);
    }

    void BackfillUnappliedRankUpRewards()
    {
        if (_save == null || Catalog == null)
            return;

        for (var r = 0; r < _save.currentRankIndex; r++)
        {
            if (!Catalog.TryGetRank(r, out var rank))
                continue;
            if (_save.rankUpRewardsAppliedThroughRankIndex >= rank.rankIndex)
                continue;
            ApplyRankUpRewards(rank);
        }

        if (_save.pendingRankUpCelebration
            && _activeRank != null
            && _save.rankUpRewardsAppliedThroughRankIndex < _activeRank.rankIndex)
        {
            ApplyRankUpRewards(_activeRank);
        }
    }

    public void CheckRankUpCondition()
    {
        if (_activeRank == null || _save == null)
            return;

        if (!AllActiveTrialsCompleted())
            return;

        TryAdvanceRank();
    }

    void TryAdvanceRank()
    {
        if (_save == null || Catalog == null)
            return;

        ApplyRankUpRewards(_activeRank);

        if (!Catalog.TryGetNextRank(_save.currentRankIndex, out var next))
        {
            Persist();
            NotifyChanged();
            return;
        }

        _save.currentRankIndex = next.rankIndex;
        OnRankUp?.Invoke(next);
        SyncActiveRankAndTrials();
        RebuildEventSubscriptions();
        Persist();
        NotifyChanged();
    }

    void ApplyRankUpRewards(PlayerRankSO rank)
    {
        if (rank?.rankUpRewards == null || _save == null)
            return;

        if (_save.rankUpRewardsAppliedThroughRankIndex >= rank.rankIndex)
            return;

        for (var i = 0; i < rank.rankUpRewards.Count; i++)
            ProgressionRewardRegistry.Apply(_save, rank.rankUpRewards[i], _activeTemplate, Catalog);

        _save.rankUpRewardsAppliedThroughRankIndex = rank.rankIndex;
    }

    void ApplyTrialReward(PlayerTrialSO trial)
    {
        if (trial?.completionRewards == null || _save == null)
            return;

        for (var i = 0; i < trial.completionRewards.Count; i++)
            ProgressionRewardRegistry.Apply(_save, trial.completionRewards[i], _activeTemplate, Catalog);
    }

    static ProgressionCatalogSO ResolveCatalogForCharacter(PlayerDataSO template)
    {
        if (template == null)
            return null;

        if (template.progressionCatalog != null)
            return template.progressionCatalog;

        return TryGetRuntime()?.defaultCatalog;
    }

    void ClampRankIndex()
    {
        if (_save == null || Catalog == null)
            return;

        var max = Catalog.MaxRankIndex();
        _save.currentRankIndex = Mathf.Clamp(_save.currentRankIndex, 0, max);
    }

    void SyncActiveRankAndTrials()
    {
        _trialStateById.Clear();
        _activeRank = Catalog.GetRankOrNull(_save.currentRankIndex);

        if (_activeRank?.associatedTrials == null)
            return;

        _save.activeTrialStates ??= new List<TrialSaveData>();

        for (var i = 0; i < _activeRank.associatedTrials.Count; i++)
        {
            var trial = _activeRank.associatedTrials[i];
            if (trial == null || ProgressionContentIds.IsNullOrEmpty(trial.TrialId))
                continue;

            if (IsTrialCompletedGlobally(trial.TrialId))
            {
                EnsureCompletedState(trial.TrialId);
                continue;
            }

            var state = new TrialSaveData
            {
                trialID = trial.TrialId,
                currentValue = GetLifetimeAccumulated(trial),
                isCompleted = false
            };

            if (state.currentValue >= trial.targetValue)
                TryCompleteTrial(trial, ref state);

            _trialStateById[trial.TrialId] = state;
        }

        RebuildActiveTrialListFromDictionary();
        EnsurePendingRankUpCelebrationState();
    }

    /// <summary>
    /// Trials finished during a run are stored in <see cref="ProgressionProfileSaveData.completedTrialIDs"/>.
    /// On reload we restore them via <see cref="EnsureCompletedState"/> without calling <see cref="TryCompleteTrial"/>,
    /// so <see cref="ProgressionProfileSaveData.pendingRankUpCelebration"/> must be reconciled here.
    /// </summary>
    void EnsurePendingRankUpCelebrationState()
    {
        if (_save == null || Catalog == null || _activeRank == null)
            return;

        if (!AllActiveTrialsCompleted())
            return;

        if (!Catalog.TryGetNextRank(_save.currentRankIndex, out _))
            return;

        if (_save.pendingRankUpCelebration)
            return;

        _save.pendingRankUpCelebration = true;
        Persist();
    }

    void EnsureCompletedState(string trialId)
    {
        _trialStateById[trialId] = new TrialSaveData
        {
            trialID = trialId,
            currentValue = 0,
            isCompleted = true
        };
    }

    bool IsTrialCompletedGlobally(string trialId)
    {
        if (_save?.completedTrialIDs == null)
            return false;

        for (var i = 0; i < _save.completedTrialIDs.Count; i++)
        {
            if (string.Equals(_save.completedTrialIDs[i], trialId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    void RebuildActiveTrialListFromDictionary()
    {
        _save.activeTrialStates = new List<TrialSaveData>();
        foreach (var kv in _trialStateById)
            _save.activeTrialStates.Add(kv.Value);
    }

    bool AllActiveTrialsCompleted()
    {
        if (_activeRank?.associatedTrials == null)
            return true;

        for (var i = 0; i < _activeRank.associatedTrials.Count; i++)
        {
            var trial = _activeRank.associatedTrials[i];
            if (trial == null)
                continue;

            if (!IsTrialCompletedGlobally(trial.TrialId))
                return false;
        }

        return true;
    }

    void RebuildEventSubscriptions()
    {
        UnsubscribeAll();
        _subscribedTrialTypes.Clear();

        if (_activeRank?.associatedTrials == null)
            return;

        foreach (var kv in _trialStateById)
        {
            if (kv.Value.isCompleted)
                continue;

            var trial = FindTrialDefinition(kv.Key);
            if (trial == null)
                continue;

            if (_subscribedTrialTypes.Add(trial.type))
                SubscribeForTrialType(trial.type);
        }
    }

    PlayerTrialSO FindTrialDefinition(string trialId)
    {
        if (_activeRank?.associatedTrials == null)
            return null;

        for (var i = 0; i < _activeRank.associatedTrials.Count; i++)
        {
            var t = _activeRank.associatedTrials[i];
            if (t != null && string.Equals(t.TrialId, trialId, StringComparison.Ordinal))
                return t;
        }

        return null;
    }

    void SubscribeForTrialType(TrialType type)
    {
        switch (type)
        {
            case TrialType.MonstersKilled:
            case TrialType.EliteKills:
            case TrialType.BossKills:
                ProgressionEvents.OnEnemyDefeated += HandleEnemyDefeated;
                break;
            case TrialType.CoinsSpend:
                ProgressionEvents.OnCoinsSpent += HandleCoinsSpent;
                break;
            case TrialType.PerfectCast:
                ProgressionEvents.OnPerfectCast += HandlePerfectCast;
                break;
            case TrialType.DamageBlocked:
                ProgressionEvents.OnDamageBlocked += HandleDamageBlocked;
                break;
            case TrialType.MovesOnMap:
                ProgressionEvents.OnMapTileMoved += HandleMapTileMoved;
                break;
            case TrialType.HpLost:
                ProgressionEvents.OnHpLost += HandleHpLost;
                break;
            case TrialType.PhysicalDamageDealt:
                ProgressionEvents.OnPhysicalDamageDealt += HandlePhysicalDamageDealt;
                break;
            case TrialType.FireDamageDealt:
                ProgressionEvents.OnFireDamageDealt += HandleFireDamageDealt;
                break;
            case TrialType.ExactRoll:
                ProgressionEvents.OnExactRoll += HandleExactRoll;
                break;
            case TrialType.AccumulatedPower:
                ProgressionEvents.OnAccumulatedPower += HandleAccumulatedPower;
                break;
            case TrialType.CastOverload:
                ProgressionEvents.OnCastOverload += HandleCastOverload;
                break;
        }
    }

    void UnsubscribeAll()
    {
        ProgressionEvents.OnEnemyDefeated -= HandleEnemyDefeated;
        ProgressionEvents.OnCoinsSpent -= HandleCoinsSpent;
        ProgressionEvents.OnPerfectCast -= HandlePerfectCast;
        ProgressionEvents.OnDamageBlocked -= HandleDamageBlocked;
        ProgressionEvents.OnMapTileMoved -= HandleMapTileMoved;
        ProgressionEvents.OnHpLost -= HandleHpLost;
        ProgressionEvents.OnPhysicalDamageDealt -= HandlePhysicalDamageDealt;
        ProgressionEvents.OnFireDamageDealt -= HandleFireDamageDealt;
        ProgressionEvents.OnExactRoll -= HandleExactRoll;
        ProgressionEvents.OnAccumulatedPower -= HandleAccumulatedPower;
        ProgressionEvents.OnCastOverload -= HandleCastOverload;
    }

    void HandleEnemyDefeated(string contentId, EnemyRank rank)
    {
        IncrementTrials(TrialType.MonstersKilled, 1);
        if (rank == EnemyRank.Elite)
            IncrementTrials(TrialType.EliteKills, 1);
        if (rank == EnemyRank.Boss)
            IncrementTrials(TrialType.BossKills, 1);
    }

    void HandleCoinsSpent(int amount, ProgressionCoinSpendSource source)
    {
        if (source is ProgressionCoinSpendSource.Shop or ProgressionCoinSpendSource.UnknownMapEvent)
            IncrementTrials(TrialType.CoinsSpend, amount);
    }

    void HandlePerfectCast() => IncrementTrials(TrialType.PerfectCast, 1);
    void HandleDamageBlocked(int amount) => IncrementTrials(TrialType.DamageBlocked, amount);
    void HandleMapTileMoved() => IncrementTrials(TrialType.MovesOnMap, 1);
    void HandleHpLost(int amount) => IncrementTrials(TrialType.HpLost, amount);
    void HandlePhysicalDamageDealt(int amount) => IncrementTrials(TrialType.PhysicalDamageDealt, amount);
    void HandleFireDamageDealt(int amount) => IncrementTrials(TrialType.FireDamageDealt, amount);
    void HandleCastOverload() => IncrementTrials(TrialType.CastOverload, 1);

    void HandleExactRoll(int rolledFaceValue)
    {
        AddLifetimeAccumulated(TrialType.ExactRoll, 1, rolledFaceValue);
        RefreshActiveTrialsForType(TrialType.ExactRoll, rolledFaceValue);
        Persist();
    }

    void HandleAccumulatedPower(int powerFromFace) => IncrementTrials(TrialType.AccumulatedPower, powerFromFace);

    void IncrementTrials(TrialType type, int delta)
    {
        if (delta <= 0)
            return;

        AddLifetimeAccumulated(type, delta, 0);
        RefreshActiveTrialsForType(type, exactRollFilter: null);
        Persist();
    }

    void RefreshActiveTrialsForType(TrialType type, int? exactRollFilter)
    {
        var keys = new List<string>(_trialStateById.Keys);
        for (var i = 0; i < keys.Count; i++)
        {
            var id = keys[i];
            var state = _trialStateById[id];
            if (state.isCompleted)
                continue;

            var trial = FindTrialDefinition(id);
            if (trial == null || trial.type != type)
                continue;

            if (type == TrialType.ExactRoll)
            {
                if (!exactRollFilter.HasValue || trial.exactRollValue != exactRollFilter.Value)
                    continue;
            }

            state.currentValue = GetLifetimeAccumulated(trial);
            _trialStateById[id] = state;
            TryCompleteTrial(trial, ref state);
            _trialStateById[id] = state;
        }
    }

    static string LifetimeCounterKey(TrialType type, int exactRollValue) =>
        type == TrialType.ExactRoll ? $"{type}:{exactRollValue}" : type.ToString();

    static int LifetimeExactRollValue(PlayerTrialSO trial) =>
        trial != null && trial.type == TrialType.ExactRoll ? trial.exactRollValue : 0;

    void ReloadProgressFromStorage()
    {
        if (_activeTemplate == null)
            return;

        _save = ProgressionSaveService.Load(_activeTemplate.MetaSaveId);
        ClampRankIndex();
        RebuildLifetimeCacheFromSave();
        AbsorbSavedTrialStatesIntoLifetime();
    }

    void RebuildLifetimeCacheFromSave()
    {
        _lifetimeAccumulated.Clear();
        if (_save?.lifetimeTrialCounters == null)
            return;

        for (var i = 0; i < _save.lifetimeTrialCounters.Count; i++)
        {
            var entry = _save.lifetimeTrialCounters[i];
            var key = LifetimeCounterKey(entry.type, entry.exactRollValue);
            if (!_lifetimeAccumulated.TryGetValue(key, out var existing))
                existing = 0;
            _lifetimeAccumulated[key] = Mathf.Max(existing, entry.accumulatedValue);
        }
    }

    /// <summary>Merges per-trial save rows into lifetime counters (covers saves written before lifetime persistence).</summary>
    void AbsorbSavedTrialStatesIntoLifetime()
    {
        if (_save?.activeTrialStates == null)
            return;

        for (var i = 0; i < _save.activeTrialStates.Count; i++)
        {
            var state = _save.activeTrialStates[i];
            var trial = FindTrialInCatalog(state.trialID);
            if (trial == null)
                continue;
            MergeLifetimeCounter(trial, state.currentValue);
        }
    }

    void SyncLifetimeCountersToSaveList()
    {
        if (_save == null)
            return;

        _save.lifetimeTrialCounters = new List<TrialTypeLifetimeCounter>();
        foreach (var kv in _lifetimeAccumulated)
        {
            if (!TryParseLifetimeCounterKey(kv.Key, out var type, out var exactRoll))
                continue;

            _save.lifetimeTrialCounters.Add(new TrialTypeLifetimeCounter
            {
                type = type,
                exactRollValue = exactRoll,
                accumulatedValue = kv.Value
            });
        }
    }

    static bool TryParseLifetimeCounterKey(string key, out TrialType type, out int exactRoll)
    {
        exactRoll = 0;
        type = default;
        if (string.IsNullOrEmpty(key))
            return false;

        var colon = key.IndexOf(':');
        if (colon >= 0)
        {
            var typeName = key.Substring(0, colon);
            if (!Enum.TryParse(typeName, out type))
                return false;
            return int.TryParse(key.Substring(colon + 1), out exactRoll);
        }

        return Enum.TryParse(key, out type);
    }

    bool MigrateLifetimeCountersFromLegacySave()
    {
        if (_save == null || _save.saveVersion >= 2)
            return false;

        if (_save.activeTrialStates != null)
        {
            for (var i = 0; i < _save.activeTrialStates.Count; i++)
            {
                var state = _save.activeTrialStates[i];
                var trial = FindTrialInCatalog(state.trialID);
                if (trial == null)
                    continue;
                MergeLifetimeCounter(trial, state.currentValue);
            }
        }

        if (_save.completedTrialIDs != null)
        {
            for (var i = 0; i < _save.completedTrialIDs.Count; i++)
            {
                var trial = FindTrialInCatalog(_save.completedTrialIDs[i]);
                if (trial == null)
                    continue;
                MergeLifetimeCounter(trial, trial.targetValue);
            }
        }

        _save.saveVersion = 2;
        SyncLifetimeCountersToSaveList();
        return true;
    }

    void MergeLifetimeCounter(PlayerTrialSO trial, int value)
    {
        if (trial == null || value <= 0)
            return;

        var key = LifetimeCounterKey(trial.type, LifetimeExactRollValue(trial));
        if (!_lifetimeAccumulated.TryGetValue(key, out var existing))
            existing = 0;
        _lifetimeAccumulated[key] = Mathf.Max(existing, value);
    }

    int GetLifetimeAccumulated(PlayerTrialSO trial)
    {
        if (trial == null)
            return 0;

        var key = LifetimeCounterKey(trial.type, LifetimeExactRollValue(trial));
        return _lifetimeAccumulated.TryGetValue(key, out var value) ? value : 0;
    }

    int AddLifetimeAccumulated(TrialType type, int delta, int exactRollValue)
    {
        if (delta <= 0)
            return GetLifetimeAccumulated(type, exactRollValue);

        var key = LifetimeCounterKey(type, type == TrialType.ExactRoll ? exactRollValue : 0);
        if (!_lifetimeAccumulated.TryGetValue(key, out var total))
            total = 0;
        total += delta;
        _lifetimeAccumulated[key] = total;
        return total;
    }

    int GetLifetimeAccumulated(TrialType type, int exactRollValue)
    {
        var key = LifetimeCounterKey(type, type == TrialType.ExactRoll ? exactRollValue : 0);
        return _lifetimeAccumulated.TryGetValue(key, out var value) ? value : 0;
    }

    PlayerTrialSO FindTrialInCatalog(string trialId)
    {
        if (Catalog?.ranks == null || ProgressionContentIds.IsNullOrEmpty(trialId))
            return null;

        for (var r = 0; r < Catalog.ranks.Count; r++)
        {
            var rank = Catalog.ranks[r];
            if (rank?.associatedTrials == null)
                continue;

            for (var t = 0; t < rank.associatedTrials.Count; t++)
            {
                var trial = rank.associatedTrials[t];
                if (trial != null && string.Equals(trial.TrialId, trialId, StringComparison.Ordinal))
                    return trial;
            }
        }

        return null;
    }

    void TryCompleteTrial(PlayerTrialSO trial, ref TrialSaveData state)
    {
        if (trial == null || state.isCompleted)
            return;

        if (state.currentValue < trial.targetValue)
            return;

        state.currentValue = trial.targetValue;
        state.isCompleted = true;
        _trialStateById[trial.TrialId] = state;

        _save.completedTrialIDs ??= new List<string>();
        if (!IsTrialCompletedGlobally(trial.TrialId))
            _save.completedTrialIDs.Add(trial.TrialId);

        ApplyTrialReward(trial);
        QueueTrialCelebration(trial.TrialId);
        if (AllActiveTrialsCompleted())
        {
            _save.pendingRankUpCelebration = true;
            ApplyRankUpRewards(_activeRank);
        }

        OnTrialCompleted?.Invoke(trial);
        RebuildEventSubscriptions();
        Persist();
        NotifyChanged();
    }

    void QueueTrialCelebration(string trialId)
    {
        if (_save == null || ProgressionContentIds.IsNullOrEmpty(trialId))
            return;

        _save.unacknowledgedTrialIds ??= new List<string>();
        if (IsTrialUnacknowledged(trialId))
            return;

        _save.unacknowledgedTrialIds.Add(trialId);
    }

    bool IsTrialUnacknowledged(string trialId)
    {
        if (_save?.unacknowledgedTrialIds == null)
            return false;

        for (var i = 0; i < _save.unacknowledgedTrialIds.Count; i++)
        {
            if (string.Equals(_save.unacknowledgedTrialIds[i], trialId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    void Persist()
    {
        if (_activeTemplate == null || _save == null)
            return;

        RebuildActiveTrialListFromDictionary();
        SyncLifetimeCountersToSaveList();
        ProgressionSaveService.Save(_activeTemplate.MetaSaveId, _save);
    }

    void NotifyChanged()
    {
        if (_activeTemplate == null)
            return;

        OnProgressionChanged?.Invoke(_activeTemplate);
        OnCharacterProgressionChanged?.Invoke(_activeTemplate);
    }

    /// <summary>Deletes all saved rank/trial data and reloads a fresh profile for the active character (if any).</summary>
    public static void ClearAllSavedProgress()
    {
        ProgressionSaveService.DeleteAll();
        TryGetRuntime()?.ReloadAfterSaveWipe();
    }

    void ReloadAfterSaveWipe()
    {
        UnsubscribeAll();
        _subscribedTrialTypes.Clear();
        _trialStateById.Clear();
        _lifetimeAccumulated.Clear();

        var template = _activeTemplate;
        _activeTemplate = null;
        _save = null;
        _activeRank = null;

        if (template != null)
            InitializeForCharacter(template);
    }
}
