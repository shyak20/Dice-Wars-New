using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CombatManager : MonoBehaviour
{
    [Header("Participants")]
    public PlayerStatus player;
    public EnemyController activeEnemy;

    [Header("Data & Physics")]
    public DiceSpawner spawner;

    [Header("UI Panels")]
    [SerializeField] private PrecisionPanel precisionPanel;
    [Tooltip("Optional. When assigned, perfect strike waits for jackpot UI (overlay + ×multiplier on pools) before SubmitTurn.")]
    [SerializeField] private JackpotPresentationController jackpotPresentation;

    [Tooltip("Optional. After turn resolution (and perfect-strike presentation if any), the orb flies to the enemy if the turn has attack/burn to the enemy; otherwise to the player's support anchor (armor-only etc.). Physical damage applies on arrival; burn ticks stay on their own timing but use Burn presentation.")]
    [SerializeField] private PowerReactiveEffectController powerOrbVisual;

    [Header("Enemy turn intro")]
    [Tooltip("Sequence: player armor → orb VFX + physical (+ thorns) → TickTurnStart (burn, etc.) → enemy armor reset → this delay → Enemy Turn banner (optional) → Enemy Turn + pause → TickBeforeEnemyTurn → attacks.\nWaits after all damage to the enemy from the player round and related FX.")]
    [SerializeField, Min(0f)] private float enemyTurnIntroDelayAfterPlayerDamageSeconds = 0.35f;
    [Tooltip("Optional. Enabled for Enemy Turn Intro Duration Seconds after the delay above, then disabled before enemy actions.")]
    [SerializeField] private GameObject enemyTurnIntroRoot;
    [SerializeField, Min(0f)] private float enemyTurnIntroDurationSeconds = 2f;

    [Header("Status Effect UI")]
    [SerializeField] private StatusEffectBarUI playerStatusBar;
    [SerializeField] private StatusEffectBarUI enemyStatusBar;

    [Header("Testing")]
    [SerializeField] private TestStartingFacesSO testStartingFaces;

    [Header("UI Icons")]
    [Tooltip("Central icon index (also register on RunManager if combat is not the first scene).")]
    [SerializeField] private GameIconIndexSO gameIconIndex;

    [Header("Reroll (RerollDie action)")]
    [Tooltip("When a face has Reroll Die, after the batch settles the player may pick a die to rethrow (or skip).")]
    [SerializeField] private RerollDieSelectionController rerollDieSelection;

    private CombatState currentState;
    private List<DieAssetSO> selectedDice = new List<DieAssetSO>();

    private int currentPower;
    private int maxPower;
    private int overchargeBonus;
    private int appliedMultiplier;
    private int bonusDamageFromActions;
    private int bonusArmorFromActions;
    private bool bustProtected;
    private bool kineticShieldActive;
    private int kineticShieldBonus;

    private List<FaceResult> channeledFaces = new List<FaceResult>();
    private List<Action<GameActionContext>> turnEndActions = new List<Action<GameActionContext>>();
    private struct PrecisionChoiceEntry
    {
        public int Amount;
        public PrecisionPromptPresentation Presentation;
    }

    private readonly Queue<PrecisionChoiceEntry> pendingPrecisionChoices = new Queue<PrecisionChoiceEntry>();

    private int expectedDiceCount = 0;
    private int pendingRollVisualSequences;

    private int rollsRemaining;
    private int maxRolls;
    private bool currentBatchIsFirstRollOfTurn;

    /// <summary>Top face per die index for the current batch (physics); committed to combat after special-effects phase.</summary>
    private DieFaceSO[] _pendingTopFaceByDieIndex;

    private Transform[] _pendingDieSourceByIndex;
    private bool _rollBatchPipelineRunning;

    private int _pendingRerollGrants;
    private bool _appliedTestStartingFaces;
    private RelicRuntimeState _relicRuntime;

    private readonly TurnRegistry _turnRegistry = new TurnRegistry();
    private readonly List<ValueBasedRollWatcherEntry> _sameTurnValueWatchers = new List<ValueBasedRollWatcherEntry>();
    private readonly List<ValueBasedRollWatcherEntry> _entireCombatValueWatchers = new List<ValueBasedRollWatcherEntry>();
    private int _rollBatchId;
    /// <summary>Increments once per settled die (any batch). Used for face-registered value watchers so later dice in the same roll batch can match.</summary>
    private int _faceResolveSequence;
    private bool _echoSkipsPowerThisBatch;

    /// <summary>Volatile turn blackboard (physical/armor/burn totals, Brute Force, Supernova).</summary>
    public TurnRegistry TurnRegistry => _turnRegistry;

    /// <summary>Increments once per player roll command (batch). Used by <see cref="AddValueBasedOnRollDuration.SameTurn"/> / <see cref="AddValueBasedOnRollDuration.EntireCombat"/> watchers.</summary>
    public int CurrentRollBatchId => _rollBatchId;

    // Updated summation logic to pull from FaceResult properties
    public int GetPendingAttack() => channeledFaces.Sum(f => f.Damage) + bonusDamageFromActions;
    public int GetPendingDefense() => channeledFaces.Sum(f => f.Armor) + kineticShieldBonus + bonusArmorFromActions;

    public List<FaceResult> GetChanneledFaces() => channeledFaces;

    /// <summary>Stored-actions bar: face attack/defense plus deferred <see cref="IGameAction"/> pool rows (status actions scale with Perfect Strike separately).</summary>
    private Dictionary<PoolRowKey, int> BuildStoredActionsPool()
    {
        var pools = new Dictionary<PoolRowKey, int>();

        void Add(PoolRowKey key, int v)
        {
            if (v == 0) return;
            pools.TryGetValue(key, out var cur);
            pools[key] = cur + v;
        }

        foreach (var face in channeledFaces)
        {
            Add(PoolRowKey.FromDieType(DieType.Damage), face.Damage);
            Add(PoolRowKey.FromDieType(DieType.Armor), face.Armor);

            if (face.ActionPoolContributions == null) continue;
            foreach (var extra in face.ActionPoolContributions)
                Add(extra.PoolKey, extra.Amount);
        }

        Add(PoolRowKey.FromDieType(DieType.Damage), bonusDamageFromActions);
        Add(PoolRowKey.FromDieType(DieType.Armor), kineticShieldBonus);
        Add(PoolRowKey.FromDieType(DieType.Armor), bonusArmorFromActions);
        return pools;
    }

    private Dictionary<PoolRowKey, int> SnapshotStoredActionsPool()
    {
        var src = BuildStoredActionsPool();
        var copy = new Dictionary<PoolRowKey, int>();
        foreach (var kvp in src)
            copy[kvp.Key] = kvp.Value;
        return copy;
    }

    private void NotifyStoredActionsPoolUpdated() =>
        CombatEvents.OnStoredActionsPoolUpdated?.Invoke(BuildStoredActionsPool());

    private void NotifyAllStoredActionsPoolUI()
    {
        var pools = BuildStoredActionsPool();
        CombatEvents.OnStoredActionsPoolUpdated?.Invoke(pools);
        CombatEvents.OnStoredActionsPoolIconsFullResync?.Invoke(pools);
    }

    public void AddOvercharge(int amount) => overchargeBonus += amount;
    public int GetAppliedMultiplier() => appliedMultiplier;
    public void SetBustProtected() => bustProtected = true;
    public void ActivateKineticShield() => kineticShieldActive = true;
    public void QueuePrecisionChoice(int amount) =>
        QueuePrecisionChoice(amount, PrecisionPromptPresentation.Default);

    public void QueuePrecisionChoice(int amount, PrecisionPromptPresentation presentation)
    {
        pendingPrecisionChoices.Enqueue(new PrecisionChoiceEntry { Amount = amount, Presentation = presentation });
    }
    public void QueueTurnEndAction(Action<GameActionContext> action) => turnEndActions.Add(action);
    public bool IsResolvingFirstRollOfTurn() => currentBatchIsFirstRollOfTurn;
    public void AddBonusDamageFromAction(int amount)
    {
        if (amount <= 0) return;
        bonusDamageFromActions += amount;
    }
    public void AddBonusArmorFromAction(int amount)
    {
        if (amount <= 0) return;
        bonusArmorFromActions += amount;
    }

    private PlayerDataSO playerData;

    private void Awake()
    {
        if (spawner == null || player == null || activeEnemy == null)
            Debug.LogError("CombatManager: Missing references!");
        if (gameIconIndex != null)
            GameIconCatalog.Register(gameIconIndex);
    }

    private void Start()
    {
        InitializeEnemy();
        InitializeCombat();
    }

    private void InitializeEnemy()
    {
        if (RunEncounterBuffer.TryConsumePendingEnemy(out var mapEnemy))
        {
            activeEnemy.Initialize(mapEnemy);
            BindStatusBars();
            return;
        }

        if (RunManager.Instance != null && RunManager.Instance.UseMapBasedRun)
        {
            Debug.LogError("CombatManager: Map-based run expected a pending enemy from RunEncounterBuffer.");
            return;
        }

        if (RunManager.Instance != null)
        {
            var room = RunManager.Instance.CurrentRoom;
            if (room == null || room.roomType != RoomType.Combat) return;
            activeEnemy.Initialize(room.enemyType);
            BindStatusBars();
            return;
        }
        if (activeEnemy.enemyData != null) activeEnemy.Initialize(activeEnemy.enemyData);
        BindStatusBars();
    }

    private void OnEnable()
    {
        CombatEvents.OnDieToggled += HandleDieToggle;
        CombatEvents.OnRollCommand += ExecuteBatchRoll;
        CombatEvents.OnBustResolved += ResolveBust;
        CombatEvents.OnEndTurnPressed += ManualEndTurn;
        CombatEvents.OnCheatWinPressed += CheatWinCombat;
        CombatEvents.OnCheatPerfectStrikePressed += ForcePerfectStrikeCheat;
    }

    private void OnDisable()
    {
        CombatEvents.OnDieToggled -= HandleDieToggle;
        CombatEvents.OnRollCommand -= ExecuteBatchRoll;
        CombatEvents.OnBustResolved -= ResolveBust;
        CombatEvents.OnEndTurnPressed -= ManualEndTurn;
        CombatEvents.OnCheatWinPressed -= CheatWinCombat;
        CombatEvents.OnCheatPerfectStrikePressed -= ForcePerfectStrikeCheat;
    }

    private void InitializeCombat()
    {
        if (PlayerDataContainer.Instance == null) return;
        playerData = PlayerDataContainer.Instance.RuntimeData;
        ApplyTestStartingFaces();
        if (player != null && playerData != null)
            player.ApplyStartingHealthFromPlayerData(playerData);
        if (RunManager.Instance != null)
            RunManager.Instance.ApplyRunVitalityToPlayerIfAny(player);
        _relicRuntime = new RelicRuntimeState();
        ResetStats();
        RelicActionRunner.RunPhase(this, RelicPhases.CombatStart);
        ChangeState(CombatState.WaitingForRoll);
    }

    public GameActionContext BuildRelicContext(FaceResult face)
    {
        return new GameActionContext
        {
            CombatManager = this,
            Player = player,
            Enemy = activeEnemy,
            ChanneledFaces = channeledFaces,
            TriggeringFace = face,
            PlayerData = playerData,
            RelicRuntime = _relicRuntime,
            CurrentPower = currentPower,
            MaxPower = maxPower
        };
    }

    private void ResetStats()
    {
        _turnRegistry.ResetVolatile();
        _entireCombatValueWatchers.Clear();
        _rollBatchId = 0;
        _faceResolveSequence = 0;
        selectedDice.Clear();
        channeledFaces.Clear();
        _pendingRerollGrants = 0;
        _rollBatchPipelineRunning = false;
        _pendingTopFaceByDieIndex = null;
        _pendingDieSourceByIndex = null;
        overchargeBonus = 0;
        appliedMultiplier = 1;
        bustProtected = false;
        kineticShieldActive = false;
        kineticShieldBonus = 0;
        bonusDamageFromActions = 0;
        bonusArmorFromActions = 0;
        pendingPrecisionChoices.Clear();
        currentPower = 0;
        maxRolls = playerData.maxRollsPerTurn;
        rollsRemaining = maxRolls;
        currentBatchIsFirstRollOfTurn = false;
        CalculateMaxPower();
        NotifyAllStoredActionsPoolUI();
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);

        BindStatusBars();

        CombatEvents.OnImmediateGameActionBarClear?.Invoke();
        CombatEvents.OnStoredActionsPoolRuntimeIconsClear?.Invoke();
    }

    private void BindStatusBars()
    {
        if (playerStatusBar != null && player != null && player.StatusEffects != null)
            playerStatusBar.Bind(player.StatusEffects);

        if (enemyStatusBar != null && activeEnemy != null && activeEnemy.StatusEffects != null)
            enemyStatusBar.Bind(activeEnemy.StatusEffects);
    }

    private void ApplyTestStartingFaces()
    {
#if UNITY_EDITOR
        if (_appliedTestStartingFaces) return;
        _appliedTestStartingFaces = true;
        if (testStartingFaces == null || !testStartingFaces.isActive) return;
        var validFaces = testStartingFaces.testFaces.FindAll(f => f != null);
        for (var d = 0; d < playerData.currentDeck.Count; d++)
        {
            var die = playerData.currentDeck[d];
            for (var i = 0; i < die.faces.Length; i++)
                die.faces[i] = validFaces[i % validFaces.Count];
        }
#endif
    }

    private int GetPerfectStrikeBaseMultiplier()
    {
        if (playerData == null)
        {
            Debug.LogError("CombatManager.GetPerfectStrikeBaseMultiplier: playerData is null.");
            return 2;
        }

        return Mathf.Max(1, playerData.perfectStrikeBaseMultiplier);
    }

    private void CalculateMaxPower()
    {
        if (playerData == null)
        {
            Debug.LogError("CombatManager.CalculateMaxPower: playerData is null.");
            maxPower = 12;
            CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
            return;
        }

        maxPower = playerData.baseMaxPower;
        if (RunManager.Instance != null)
            maxPower += RunManager.Instance.RunShrineBonusMaxPower;
        if (playerData?.currentDeck != null)
        {
            for (int i = 2; i < playerData.currentDeck.Count; i++)
            {
                var die = playerData.currentDeck[i];
                if (die != null)
                    maxPower += die.MaxPowerContribution;
            }
        }

        maxPower += RelicActionRunner.QueryIntSum(RelicPhases.QueryMaxPowerBonus, this);

        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
    }

    private void HandleDieToggle(DieAssetSO die)
    {
        if (currentState != CombatState.WaitingForRoll) return;
        if (selectedDice.Contains(die)) selectedDice.Remove(die);
        else selectedDice.Add(die);
    }

    private void ExecuteBatchRoll()
    {
        if (currentState != CombatState.WaitingForRoll || selectedDice.Count == 0) return;
        _rollBatchId++;
        _pendingRerollGrants = 0;
        _echoSkipsPowerThisBatch = player != null &&
                                   player.StatusEffects.TryConsumeEchoPowerSkipForNextRollBatch(BuildStatusContext());
        expectedDiceCount = selectedDice.Count;
        _pendingTopFaceByDieIndex = new DieFaceSO[expectedDiceCount];
        _pendingDieSourceByIndex = new Transform[expectedDiceCount];
        pendingRollVisualSequences = 0;
        _rollBatchPipelineRunning = false;
        currentBatchIsFirstRollOfTurn = (rollsRemaining == maxRolls);
        rollsRemaining--;
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);
        ChangeState(CombatState.Rolling);
        spawner.SpawnAndRollBatch(selectedDice);
    }

    /// <summary>
    /// Physics settled on a die (batch index order). Runs special-effects phase once all dice have a face, then gather commits.
    /// </summary>
    public void OnDiePhysicsSettled(int batchIndex, DieFaceSO face, Transform dieWorldSource)
    {
        if (face == null || expectedDiceCount <= 0) return;
        if (batchIndex < 0 || batchIndex >= expectedDiceCount) return;
        if (_pendingTopFaceByDieIndex == null || _pendingTopFaceByDieIndex.Length != expectedDiceCount) return;

        _pendingTopFaceByDieIndex[batchIndex] = face;
        _pendingDieSourceByIndex[batchIndex] = dieWorldSource;

        if (_rollBatchPipelineRunning)
            return;

        if (AllPendingTopFacesFilled())
            StartCoroutine(CoRollBatchPipeline());
    }

    private bool AllPendingTopFacesFilled()
    {
        if (_pendingTopFaceByDieIndex == null) return false;
        for (var i = 0; i < expectedDiceCount; i++)
        {
            if (_pendingTopFaceByDieIndex[i] == null) return false;
        }

        return true;
    }

    private static int CountRerollGrantsOnFace(DieFaceSO face)
    {
        if (face?.actions == null) return 0;
        var n = 0;
        foreach (var a in face.actions)
        {
            if (a is RerollDieAction)
                n++;
        }

        return n;
    }

    private IEnumerator CoRollBatchPipeline()
    {
        if (spawner == null)
        {
            _rollBatchPipelineRunning = false;
            ProcessPrecisionQueue();
            yield break;
        }

        _rollBatchPipelineRunning = true;

        // --- Special effects (reroll, and later more) run before combat state receives the batch. ---
        if (rerollDieSelection == null && CountRerollGrantsFromAllPendingFaces() > 0)
            Debug.LogError("CombatManager: Reroll Die on a face but rerollDieSelection is not assigned.");

        _pendingRerollGrants = CountRerollGrantsFromAllPendingFaces();
        while (_pendingRerollGrants > 0 && rerollDieSelection != null)
        {
            var dice = spawner.GetActiveDiceSnapshot();
            if (dice.Count == 0)
            {
                Debug.LogWarning("CombatManager: Reroll — no active dice; aborting remaining reroll offers.");
                break;
            }

            var wait = true;
            var skipped = false;
            GameObject picked = null;
            rerollDieSelection.BeginSelection(dice, (sk, go) =>
            {
                skipped = sk;
                picked = go;
                wait = false;
            });
            while (wait) yield return null;

            _pendingRerollGrants = Mathf.Max(0, _pendingRerollGrants - 1);

            if (!skipped && picked != null)
            {
                var idx = spawner.GetIndexOfActiveDie(picked);
                if (idx < 0)
                    Debug.LogWarning("CombatManager: Picked die is not in the active batch.");
                else
                {
                    _pendingTopFaceByDieIndex[idx] = null;
                    _pendingDieSourceByIndex[idx] = null;
                    spawner.RerollDiePhysics(picked);
                    yield return new WaitUntil(() => _pendingTopFaceByDieIndex[idx] != null);
                    var newFace = _pendingTopFaceByDieIndex[idx];
                    if (newFace != null)
                        _pendingRerollGrants += CountRerollGrantsOnFace(newFace);
                }
            }
        }

        // --- Gather: apply resolved faces to combat (power, pools, watchers) in spawn order. ---
        var batchGatherStart = channeledFaces.Count;
        for (var i = 0; i < expectedDiceCount; i++)
        {
            var f = _pendingTopFaceByDieIndex[i];
            var t = _pendingDieSourceByIndex[i];
            if (f == null)
            {
                Debug.LogError($"CombatManager: Missing pending face at index {i} after special-effects phase.");
                continue;
            }

            CommitResolvedRoll(f, t);
        }

        QueueAddPowerChoicesAfterBatchGather(batchGatherStart, channeledFaces.Count);

        _rollBatchPipelineRunning = false;
        _pendingTopFaceByDieIndex = null;
        _pendingDieSourceByIndex = null;

        yield return new WaitUntil(() => pendingRollVisualSequences <= 0);
        ProcessPrecisionQueue();
    }

    private void QueueAddPowerChoicesAfterBatchGather(int startInclusive, int endExclusive)
    {
        for (var i = startInclusive; i < endExclusive; i++)
        {
            if (i < 0 || i >= channeledFaces.Count) continue;
            var fr = channeledFaces[i];
            if (fr?.Actions == null) continue;
            foreach (var a in fr.Actions)
            {
                if (a is AddPowerAction add)
                {
                    var n = add.PowerAmount;
                    if (n <= 0) continue;
                    QueuePrecisionChoice(n, PrecisionPromptPresentation.AddPowerAbility);
                    if (GameActionDebug.Enabled)
                        Debug.Log($"[AddPowerAction] After batch gather, queued optional +{n} power (face index {i})");
                }
            }
        }
    }

    private int CountRerollGrantsFromAllPendingFaces()
    {
        if (_pendingTopFaceByDieIndex == null) return 0;
        var n = 0;
        for (var i = 0; i < _pendingTopFaceByDieIndex.Length; i++)
        {
            var f = _pendingTopFaceByDieIndex[i];
            if (f == null) continue;
            n += CountRerollGrantsOnFace(f);
        }

        return n;
    }

    private void CommitResolvedRoll(DieFaceSO face, Transform dieWorldSource)
    {
        _faceResolveSequence++;

        bool kineticArmorThisRoll = kineticShieldActive;
        if (kineticArmorThisRoll) kineticShieldBonus++;

        var statusCtx = BuildStatusContext();
        var modifiedValue = player.StatusEffects.ModifyFaceValue(statusCtx, face.value);
        var rolledDamage = face.damage;
        if (rolledDamage > 0)
            rolledDamage += player.StatusEffects.GetTotalPerDieAttackDamageBonus(statusCtx);

        var result = new FaceResult
        {
            Face = face,
            Value = modifiedValue,
            Type = face.type,
            Damage = rolledDamage,
            Armor = face.armor,
            ActivateImmediately = face.activateImmediately,
        };
        if (face.actions != null)
        {
            foreach (var a in face.actions)
                if (a != null)
                    result.Actions.Add(a);
        }

        result.DieSource = dieWorldSource;
        result.PowerContributionThisResolve = _echoSkipsPowerThisBatch ? 0 : modifiedValue;
        result.KineticShieldBonusContribution = kineticArmorThisRoll ? 1 : 0;

        ApplyQueuedNextRollMultiplier(result, _turnRegistry);

        if (face.actions != null)
        {
            foreach (var a in face.actions)
            {
                if (a is FaceResolveModifierBase mod)
                    mod.Modify(face, result, this, _turnRegistry);
            }
        }

        var relicModifyCtx = BuildRelicContext(result);
        relicModifyCtx.RelicPhase = RelicPhases.ModifyFaceResult;
        relicModifyCtx.CurrentPower = currentPower;
        relicModifyCtx.MaxPower = maxPower;
        RelicActionRunner.ExecuteAllRelics(relicModifyCtx);

        ApplyValueBasedRollWatchersArmorDamage(result);

        _turnRegistry.RecordResolvedFace(result);

        PopulateActionPoolContributions(result);

        channeledFaces.Add(result);
        if (!_echoSkipsPowerThisBatch)
            currentPower += modifiedValue;

        var relicAfterPowerCtx = BuildRelicContext(result);
        relicAfterPowerCtx.RelicPhase = RelicPhases.AfterPowerChangedFromRoll;
        relicAfterPowerCtx.CurrentPower = currentPower;
        relicAfterPowerCtx.MaxPower = maxPower;
        RelicActionRunner.ExecuteAllRelics(relicAfterPowerCtx);

        ApplyValueBasedRollWatchersBurn(result);

        if (result.ActivateImmediately && result.Actions.Count > 0)
        {
            var context = BuildContext(result);
            List<Sprite> immediateIcons = null;
            foreach (var a in result.Actions)
            {
                if (a is FaceResolveModifierBase) continue;
                if (a is RerollDieAction) continue;
                if (a is AddPowerAction) continue;
                a.Execute(context);
                var icon = GameActionIconUtility.GetDisplayIcon(a);
                if (icon != null)
                {
                    immediateIcons ??= new List<Sprite>();
                    immediateIcons.Add(icon);
                }
            }

            if (immediateIcons != null && immediateIcons.Count > 0)
                CombatEvents.OnImmediateGameActionIconsShown?.Invoke(immediateIcons);
        }

        CollectValueWatcherRegistrationsFromFace(result);

        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
        NotifyStoredActionsPoolUpdated();
        if (!result.ActivateImmediately)
            PushDeferredPoolIconHints(result);

        if (dieWorldSource != null)
        {
            var lines = BuildRollVisualLines(result, kineticArmorThisRoll);
            if (lines.Count > 0 && CombatEvents.OnDiceRollVisualFeedback != null)
            {
                pendingRollVisualSequences++;
                var payload = new DiceRollVisualPayload
                {
                    WorldAnchor = dieWorldSource.position,
                    Lines = lines
                };
                payload.BindVisualFinished(OnRollVisualSequenceFinished);
                CombatEvents.OnDiceRollVisualFeedback.Invoke(payload);
            }
        }
    }

    private void OnRollVisualSequenceFinished()
    {
        pendingRollVisualSequences--;
        if (pendingRollVisualSequences < 0)
        {
            Debug.LogError("CombatManager: pendingRollVisualSequences underflow — check DiceRollVisualPayload.ReportVisualFinished is called once per payload.");
            pendingRollVisualSequences = 0;
        }
    }

    private void PopulateActionPoolContributions(FaceResult result)
    {
        result.ActionPoolContributions.Clear();
        if (result.Actions == null || player == null) return;
        foreach (var a in result.Actions)
        {
            if (a is FaceResolveModifierBase) continue;
            if (a is ApplyStatusEffectAction apply)
                apply.AppendPoolContributionIfAny(result, player, result.ActivateImmediately);
            if (a is MaxHpAction maxHp)
                maxHp.AppendPoolContributionIfAny(result);
            if (a is AddValueBasedOnRollAction valueBonus)
                valueBonus.AppendPoolContributionIfAny(result, player);
            if (a is ThornsAction thorns)
                thorns.AppendPoolContributionIfAny(result, result.ActivateImmediately);
        }
    }

    /// <param name="fromRelicCombatStart">When true, the first player roll batch of the fight is skipped (watchers start from batch 2).</param>
    public void RegisterValueBasedRollWatcher(int requiredFaceValue, RollBonusType bonusType, int amount, BurnEffectSO burnDefinition, AddValueBasedOnRollDuration duration, bool fromRelicCombatStart = false)
    {
        if (duration == AddValueBasedOnRollDuration.SameRoll)
        {
            Debug.LogError("CombatManager.RegisterValueBasedRollWatcher: SameRoll does not use watchers.");
            return;
        }

        if (amount <= 0) return;
        if (bonusType == RollBonusType.Burn && burnDefinition == null)
        {
            Debug.LogError("CombatManager.RegisterValueBasedRollWatcher: burnDefinition required when Bonus Type is Burn.");
            return;
        }

        var firstBatch = fromRelicCombatStart
            ? Mathf.Max(2, _rollBatchId + 2)
            : Mathf.Max(1, _rollBatchId + 1);

        var entry = new ValueBasedRollWatcherEntry
        {
            RequiredFaceValue = requiredFaceValue,
            BonusType = bonusType,
            Amount = amount,
            BurnDefinition = burnDefinition,
            FirstEligibleBatchId = firstBatch,
            FirstEligibleResolveSequence = 0
        };

        if (duration == AddValueBasedOnRollDuration.SameTurn)
            _sameTurnValueWatchers.Add(entry);
        else
            _entireCombatValueWatchers.Add(entry);
    }

    /// <summary>Registers a watcher when a die face with <see cref="AddValueBasedOnRollAction"/> resolves (Same Turn / Entire Combat from dice).</summary>
    public void RegisterValueBasedRollWatcherFromDieResolution(int requiredFaceValue, RollBonusType bonusType, int amount, BurnEffectSO burnDefinition, AddValueBasedOnRollDuration duration)
    {
        if (duration == AddValueBasedOnRollDuration.SameRoll)
        {
            Debug.LogError("CombatManager.RegisterValueBasedRollWatcherFromDieResolution: SameRoll does not use watchers.");
            return;
        }

        if (amount <= 0) return;
        if (bonusType == RollBonusType.Burn && burnDefinition == null)
        {
            Debug.LogError("CombatManager.RegisterValueBasedRollWatcherFromDieResolution: burnDefinition required when Bonus Type is Burn.");
            return;
        }

        var entry = new ValueBasedRollWatcherEntry
        {
            RequiredFaceValue = requiredFaceValue,
            BonusType = bonusType,
            Amount = amount,
            BurnDefinition = burnDefinition,
            FirstEligibleBatchId = 0,
            FirstEligibleResolveSequence = _faceResolveSequence + 1
        };

        if (duration == AddValueBasedOnRollDuration.SameTurn)
            _sameTurnValueWatchers.Add(entry);
        else
            _entireCombatValueWatchers.Add(entry);
    }

    private void CollectValueWatcherRegistrationsFromFace(FaceResult face)
    {
        if (face?.Actions == null) return;
        foreach (var a in face.Actions)
        {
            if (a is FaceResolveModifierBase) continue;
            if (a is AddValueBasedOnRollAction valueBonus)
                valueBonus.RegisterWatcherIfNeeded(this);
        }
    }

    private void ApplyValueBasedRollWatchersArmorDamage(FaceResult result)
    {
        if (result == null) return;
        for (var i = 0; i < _sameTurnValueWatchers.Count; i++)
            ApplyValueBasedRollWatcherArmorDamage(result, _sameTurnValueWatchers[i]);
        for (var i = 0; i < _entireCombatValueWatchers.Count; i++)
            ApplyValueBasedRollWatcherArmorDamage(result, _entireCombatValueWatchers[i]);
    }

    private bool ValueWatcherEligible(ValueBasedRollWatcherEntry w)
    {
        if (w.FirstEligibleBatchId > 0 && _rollBatchId < w.FirstEligibleBatchId)
            return false;
        if (w.FirstEligibleResolveSequence > 0 && _faceResolveSequence < w.FirstEligibleResolveSequence)
            return false;
        return true;
    }

    private void ApplyValueBasedRollWatcherArmorDamage(FaceResult result, ValueBasedRollWatcherEntry w)
    {
        if (!ValueWatcherEligible(w)) return;
        if (result.Value != w.RequiredFaceValue) return;
        if (w.Amount <= 0) return;
        switch (w.BonusType)
        {
            case RollBonusType.Armor:
                result.Armor += w.Amount;
                break;
            case RollBonusType.Damage:
                result.Damage += w.Amount;
                break;
        }
    }

    private void ApplyValueBasedRollWatchersBurn(FaceResult result)
    {
        if (result == null || player == null) return;
        var ctx = BuildContext(result);
        for (var i = 0; i < _sameTurnValueWatchers.Count; i++)
            ApplyValueBasedRollWatcherBurn(result, ctx, _sameTurnValueWatchers[i]);
        for (var i = 0; i < _entireCombatValueWatchers.Count; i++)
            ApplyValueBasedRollWatcherBurn(result, ctx, _entireCombatValueWatchers[i]);
    }

    private void ApplyValueBasedRollWatcherBurn(FaceResult result, GameActionContext ctx, ValueBasedRollWatcherEntry w)
    {
        if (!ValueWatcherEligible(w)) return;
        if (result.Value != w.RequiredFaceValue) return;
        if (w.BonusType != RollBonusType.Burn || w.Amount <= 0) return;

        AddValueBasedOnRollAction.ApplyBurnToEnemyFromContext(ctx, w.Amount, w.BurnDefinition);
        AddValueBasedOnRollAction.TryAppendBurnPoolLine(result, player, w.RequiredFaceValue, w.Amount, w.BurnDefinition);
    }

    private static void PushDeferredPoolIconHints(FaceResult result)
    {
        if (result.ActivateImmediately) return;

        void Hint(PoolRowKey key, int amt, Sprite icon)
        {
            if (amt > 0 && icon != null)
                CombatEvents.OnRuntimePoolIconForRow?.Invoke(key, icon);
        }

        Hint(PoolRowKey.FromDieType(DieType.Damage), result.Damage, GameIconCatalog.GetElementIcon(DieType.Damage));
        Hint(PoolRowKey.FromDieType(DieType.Armor), result.Armor, GameIconCatalog.GetElementIcon(DieType.Armor));

        if (result.ActionPoolContributions == null) return;
        foreach (var extra in result.ActionPoolContributions)
            Hint(extra.PoolKey, extra.Amount, extra.Icon);
    }

    private static List<RollOutcomeVisualLine> BuildRollVisualLines(FaceResult result, bool kineticArmorThisRoll)
    {
        var lines = new List<RollOutcomeVisualLine>();

        void AddLine(PoolRowKey key, int amt, Sprite icon)
        {
            if (amt <= 0) return;
            lines.Add(new RollOutcomeVisualLine { RowKey = key, Amount = amt, IconOverride = icon });
        }

        AddLine(PoolRowKey.FromDieType(DieType.Damage), result.Damage, GameIconCatalog.GetElementIcon(DieType.Damage));
        AddLine(PoolRowKey.FromDieType(DieType.Armor), result.Armor, GameIconCatalog.GetElementIcon(DieType.Armor));

        if (result.ActionPoolContributions != null)
        {
            foreach (var extra in result.ActionPoolContributions)
            {
                if (extra.Amount <= 0) continue;
                lines.Add(new RollOutcomeVisualLine
                {
                    RowKey = extra.PoolKey,
                    Amount = extra.Amount,
                    IconOverride = extra.Icon
                });
            }
        }

        if (kineticArmorThisRoll)
            AddLine(PoolRowKey.FromDieType(DieType.Armor), 1, GameIconCatalog.GetElementIcon(DieType.Armor));

        return lines;
    }

    /// <summary>Applies queued next-roll multiplier once when eligible damage/armor channels are present.</summary>
    private static void ApplyQueuedNextRollMultiplier(FaceResult result, TurnRegistry registry)
    {
        if (!registry.NextRollMultiplierActive) return;
        if (registry.NextRollMultiplier <= 0f) return;

        bool applied = false;

        if (registry.NextRollMultiplyDamage && result.Damage > 0)
        {
            result.Damage = Mathf.Max(0, Mathf.RoundToInt(result.Damage * registry.NextRollMultiplier));
            applied = true;
        }

        if (registry.NextRollMultiplyArmor && result.Armor > 0)
        {
            result.Armor = Mathf.Max(0, Mathf.RoundToInt(result.Armor * registry.NextRollMultiplier));
            applied = true;
        }

        if (!applied) return;

        registry.NextRollMultiplierActive = false;
        registry.NextRollMultiplyDamage = false;
        registry.NextRollMultiplyArmor = false;
        registry.NextRollMultiplier = 1f;
    }

    private GameActionContext BuildContext(FaceResult triggeringFace = null)
    {
        return new GameActionContext
        {
            CombatManager = this,
            Player = player,
            Enemy = activeEnemy,
            ChanneledFaces = channeledFaces,
            TriggeringFace = triggeringFace
        };
    }

    private GameActionContext BuildEnemyActionContext(EnemyActionSO sourceIntent)
    {
        return new GameActionContext
        {
            CombatManager = this,
            Player = player,
            Enemy = activeEnemy,
            ChanneledFaces = channeledFaces,
            TriggeringFace = null,
            PlayerData = playerData,
            RelicRuntime = _relicRuntime,
            CurrentPower = currentPower,
            MaxPower = maxPower,
            SourceEnemyAction = sourceIntent
        };
    }

    private StatusEffectContext BuildStatusContext() => new StatusEffectContext { CombatManager = this, Player = player, Enemy = activeEnemy };

    /// <summary>Per-hit physical damage for enemy intent UI; matches <see cref="EnemyTurnRoutine"/> (Strength bonus, then Chill/Shattered/etc.).</summary>
    public int PreviewEnemyPhysicalHitDamage(EnemyController enemy, int intentBaseDamagePerHit)
    {
        if (enemy == null || intentBaseDamagePerHit <= 0)
            return intentBaseDamagePerHit;
        var ctx = new StatusEffectContext { CombatManager = this, Player = player, Enemy = enemy };
        var boosted = intentBaseDamagePerHit + enemy.StatusEffects.GetTotalPerDieAttackDamageBonus(ctx);
        return Mathf.Max(0, enemy.StatusEffects.ModifyEnemyHitDamage(ctx, boosted));
    }

    /// <summary>Used by face resolve modifiers and status actions that need a status context.</summary>
    public StatusEffectContext BuildStatusContextForEffects() => BuildStatusContext();

    private void ProcessPrecisionQueue()
    {
        if (pendingPrecisionChoices.Count > 0)
        {
            var entry = pendingPrecisionChoices.Dequeue();
            precisionPanel.Show(entry.Amount, entry.Presentation, accepted =>
            {
                if (accepted) { currentPower += entry.Amount; CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower); }
                ProcessPrecisionQueue();
            });
        }
        else CheckBustStatus();
    }

    private void CheckBustStatus()
    {
        var perfectAtMax = currentPower == maxPower;
        var perfectAtMaxMinusOne = maxPower > 1 && currentPower == maxPower - 1 &&
                                   RelicActionRunner.QueryBoolOr(RelicPhases.QueryPerfectAtMaxMinusOne, this);
        if (perfectAtMax || perfectAtMaxMinusOne)
        {
            var poolsBefore = SnapshotStoredActionsPool();
            appliedMultiplier = GetPerfectStrikeBaseMultiplier();
            var relicPerfect = RelicActionRunner.QueryIntMax(RelicPhases.QueryPerfectStrikeMultiplier, this);
            if (relicPerfect > 0)
                appliedMultiplier = Mathf.Max(appliedMultiplier, relicPerfect);
            appliedMultiplier += overchargeBonus;
            int jackpotMultiplier = appliedMultiplier;
            foreach (var face in channeledFaces)
            {
                face.Damage *= appliedMultiplier;
                face.Armor *= appliedMultiplier;
            }

            MultiplyPendingStrikeScaledPoolContributions(channeledFaces, appliedMultiplier);

            kineticShieldBonus *= appliedMultiplier;
            bonusDamageFromActions *= appliedMultiplier;
            bonusArmorFromActions *= appliedMultiplier;
            activeEnemy.StatusEffects.TickPerfectStrike(BuildStatusContext());
            RelicActionRunner.RunPhase(this, RelicPhases.OnPerfectStrike);
            var poolsAfter = SnapshotStoredActionsPool();
            if (CheckVictory())
            {
                NotifyAllStoredActionsPoolUI();
                return;
            }

            if (jackpotPresentation != null)
                StartCoroutine(CoFinishJackpotAfterPresentation(jackpotMultiplier, poolsBefore, poolsAfter));
            else
            {
                NotifyAllStoredActionsPoolUI();
                SubmitTurn();
            }
        }
        else if (currentPower > maxPower)
        {
            if (RelicActionRunner.TryConsumeFreeBust(this))
            {
                SubmitTurn();
                return;
            }

            if (bustProtected) SubmitTurn();
            else if (_turnRegistry.SupernovaBustOverrideActive)
            {
                if (activeEnemy != null && _turnRegistry.SupernovaBustDamage > 0)
                    activeEnemy.TakeDamage(_turnRegistry.SupernovaBustDamage);
                _turnRegistry.SupernovaBustOverrideActive = false;
                if (CheckVictory()) return;
                SubmitTurn();
            }
            else
            {
                ChangeState(CombatState.BustCheck);
                CombatEvents.OnBustOccurred?.Invoke(GetPendingAttack(), GetPendingDefense());
            }
        }
        else
        {
            if (rollsRemaining <= 0) SubmitTurn();
            else ChangeState(CombatState.WaitingForRoll);
        }
    }

    private void ManualEndTurn() { if (currentState == CombatState.WaitingForRoll) SubmitTurn(); }

    /// <summary>Cheat/debug: force current pooled faces through the Perfect Strike branch.</summary>
    private void ForcePerfectStrikeCheat()
    {
        if (currentState != CombatState.WaitingForRoll)
            return;
        if (channeledFaces == null || channeledFaces.Count == 0)
            return;

        currentPower = maxPower;
        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
        CheckBustStatus();
    }

    private void ResolveBust(bool nullifyDamage)
    {
        foreach (var face in channeledFaces)
        {
            if (nullifyDamage) face.Damage = 0;
            else face.Armor = 0;
        }
        if (nullifyDamage) bonusDamageFromActions = 0;
        else { bonusArmorFromActions = 0; kineticShieldBonus = 0; }
        ApplyBustToPendingApplyStatusPoolContributions(channeledFaces, nullifyDamage);
        NotifyAllStoredActionsPoolUI();
        SubmitTurn();
    }

    /// <summary>Status applies and Max HP rows in <see cref="FaceResult.ActionPoolContributions"/> scale with Perfect Strike (display + grant use same totals).</summary>
    private static void MultiplyPendingStrikeScaledPoolContributions(List<FaceResult> faces, int multiplier)
    {
        if (faces == null || multiplier <= 1) return;
        foreach (var face in faces)
        {
            for (var i = 0; i < face.ActionPoolContributions.Count; i++)
            {
                var c = face.ActionPoolContributions[i];
                if (c.PoolSourceAction == null && c.MaxHpPoolSource == null) continue;
                c.Amount *= multiplier;
                face.ActionPoolContributions[i] = c;
            }
        }
    }

    /// <summary>Final +max HP grant for a face action after pool lines may have been scaled by jackpot.</summary>
    public int ResolveMaxHpPoolGrant(MaxHpAction action)
    {
        if (action == null || channeledFaces == null) return 0;
        foreach (var face in channeledFaces)
        {
            foreach (var c in face.ActionPoolContributions)
            {
                if (c.MaxHpPoolSource != action) continue;
                return Mathf.Max(0, c.Amount);
            }
        }

        return Mathf.Max(0, action.Amount * Mathf.Max(1, appliedMultiplier));
    }

    /// <summary>Bust choice removes pending enemy-target applies with damage, or player-target applies with armor.</summary>
    private static void ApplyBustToPendingApplyStatusPoolContributions(List<FaceResult> faces, bool nullifyDamage)
    {
        if (faces == null) return;
        foreach (var face in faces)
        {
            for (var i = 0; i < face.ActionPoolContributions.Count; i++)
            {
                var c = face.ActionPoolContributions[i];
                var src = c.PoolSourceAction;
                if (src?.StatusEffectDefinition == null) continue;
                var target = src.StatusEffectDefinition.target;
                var remove = nullifyDamage ? target == StatusEffectTarget.Enemy : target == StatusEffectTarget.Player;
                if (!remove) continue;
                c.Amount = 0;
                face.ActionPoolContributions[i] = c;
            }
        }
    }

    private static Dictionary<ApplyStatusEffectAction, int> BuildPendingApplyStackOverrides(FaceResult face)
    {
        if (face?.Actions == null) return null;

        var hasApplyStatus = false;
        foreach (var a in face.Actions)
        {
            if (a is ApplyStatusEffectAction)
            {
                hasApplyStatus = true;
                break;
            }
        }

        if (!hasApplyStatus) return null;

        var map = new Dictionary<ApplyStatusEffectAction, int>();
        foreach (var c in face.ActionPoolContributions)
        {
            if (c.PoolSourceAction == null) continue;
            map[c.PoolSourceAction] = c.Amount;
        }

        foreach (var a in face.Actions)
        {
            if (a is ApplyStatusEffectAction apply && !map.ContainsKey(apply))
                map[apply] = 0;
        }

        return map;
    }

    private void SubmitTurn()
    {
        ChangeState(CombatState.TurnEnd);
        var ctx = BuildContext();

        // Execution of stored/deferred actions (per face, in list order)
        foreach (var face in channeledFaces)
        {
            if (face.ActivateImmediately || face.Actions == null || face.Actions.Count == 0) continue;
            var faceCtx = BuildContext(face);
            faceCtx.PendingApplyStackOverrides = BuildPendingApplyStackOverrides(face);
            foreach (var a in face.Actions)
            {
                if (a is FaceResolveModifierBase) continue;
                if (a is AddPowerAction) continue;
                if (a != null)
                    a.Execute(faceCtx);
            }
        }

        foreach (var action in turnEndActions) action.Invoke(ctx);
        turnEndActions.Clear();

        var statusCtx = BuildStatusContext();
        int pendingAttack = GetPendingAttack();
        pendingAttack += player.StatusEffects.GetTotalBonusAttack(statusCtx);
        pendingAttack = activeEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, pendingAttack);
        int pendingDefense = GetPendingDefense();

        bool enemyDamageLine = pendingAttack > 0 || _turnRegistry.BurnAppliedThisTurn > 0;
        bool usePowerOrb = powerOrbVisual != null && activeEnemy != null && player != null &&
                           (currentPower > 0 || pendingDefense > 0 || enemyDamageLine);

        if (!usePowerOrb)
            ApplyPlayerTurnCombatResults(pendingAttack, pendingDefense);
        else
        {
            bool flyOrbToEnemy = enemyDamageLine;
            Transform orbAnchor = flyOrbToEnemy
                ? activeEnemy.GetPowerOrbHitAnchor()
                : player.GetPowerOrbSupportAnchor();
            if (orbAnchor == null)
            {
                Debug.LogError("CombatManager.SubmitTurn: power orb anchor is null. Assign PlayerStatus powerOrbSupportWorldAnchor or enemy hit anchor.");
                ApplyPlayerTurnCombatResults(pendingAttack, pendingDefense);
            }
            else
            {
                bool allowZeroCombatPower = !flyOrbToEnemy || (currentPower <= 0 && enemyDamageLine);
                bool forceStartingVisibleScale = currentPower <= 0;
                StartCoroutine(CoSubmitTurnAfterOrbFlight(
                    pendingAttack,
                    pendingDefense,
                    orbAnchor,
                    flyOrbToEnemy,
                    allowZeroCombatPower,
                    forceStartingVisibleScale));
            }
        }
    }

    private IEnumerator CoSubmitTurnAfterOrbFlight(
        int pendingAttack,
        int pendingDefense,
        Transform orbAnchor,
        bool flyOrbToEnemy,
        bool allowZeroCombatPower,
        bool forceStartingVisibleScale)
    {
        if (pendingDefense > 0 && player != null)
            player.AddArmor(pendingDefense);

        bool impactAnnounced = false;
        bool attackResolved = false;
        bool continueCombat = true;

        void AnnounceOrbImpact()
        {
            if (impactAnnounced) return;
            impactAnnounced = true;
            CombatEvents.OnPowerOrbImpact?.Invoke(new PowerOrbImpactPayload(
                flyOrbToEnemy ? PowerOrbImpactTarget.Enemy : PowerOrbImpactTarget.PlayerSupport,
                orbAnchor.position,
                flyOrbToEnemy ? activeEnemy : null));
        }

        void OnOrbImpact()
        {
            AnnounceOrbImpact();
            if (attackResolved) return;
            attackResolved = true;
            if (pendingAttack > 0)
                continueCombat = ApplyPendingPlayerAttackFromTurn(pendingAttack);
            else
                continueCombat = true;
        }

        IEnumerator flight = powerOrbVisual.RunFlightToWorldAnchor(
            orbAnchor, allowZeroCombatPower, forceStartingVisibleScale, OnOrbImpact);
        while (flight.MoveNext())
            yield return flight.Current;

        if (!attackResolved)
            OnOrbImpact();
        if (!continueCombat) yield break;

        ResolveEnemyOpeningAndStartEnemyTurn();
    }

    /// <summary>Enemy damage and thorns from the player turn; does not apply armor or start the enemy turn.</summary>
    /// <returns>False if defeat or victory ended combat.</returns>
    private bool ApplyPendingPlayerAttackFromTurn(int pendingAttack)
    {
        if (pendingAttack <= 0) return true;
        activeEnemy.TakeDamage(pendingAttack, EnemyDamagePresentationKind.Physical);

        var retaliate = activeEnemy.StatusEffects.GetThornsRetaliateStacks();
        if (retaliate > 0 && player != null)
        {
            // Floating numbers: bias toward player so it reads as player damage, with enemy anchor to help screen depth after orb FX.
            var thornsPopupAnchor = Vector3.Lerp(player.GetDamageNumberWorldPosition(), activeEnemy.GetDamageNumberWorldPosition(), 0.25f);
            player.TakeDamage(retaliate, PlayerDamageSource.ThornsRetaliation, thornsPopupAnchor);
            if (CheckDefeat()) return false;
        }

        if (CheckVictory()) return false;
        return true;
    }

    /// <summary>
    /// After all player-turn damage to the enemy: <see cref="StatusEffectManager.TickTurnStart"/> (burn, decay, etc.) first,
    /// then <see cref="EnemyController.ResetArmor"/> before the enemy-turn coroutine.
    /// </summary>
    private void ResolveEnemyOpeningAndStartEnemyTurn()
    {
        if (activeEnemy != null && player != null)
        {
            var openingCtx = BuildStatusContext();
            activeEnemy.StatusEffects.TickTurnStart(openingCtx);
            if (CheckVictory()) return;
            activeEnemy.ResetArmor();
        }

        StartCoroutine(EnemyTurnRoutine());
    }

    private void ApplyPlayerTurnCombatResults(int pendingAttack, int pendingDefense)
    {
        if (pendingDefense > 0 && player != null)
            player.AddArmor(pendingDefense);

        if (!ApplyPendingPlayerAttackFromTurn(pendingAttack)) return;

        ResolveEnemyOpeningAndStartEnemyTurn();
    }

    private IEnumerator CoFinishJackpotAfterPresentation(int multiplier, Dictionary<PoolRowKey, int> poolsBefore, Dictionary<PoolRowKey, int> poolsAfter)
    {
        // Unity does not reliably run a nested IEnumerator with "yield return routine()"; must use StartCoroutine.
        yield return StartCoroutine(jackpotPresentation.Run(multiplier, poolsBefore, poolsAfter));
        NotifyAllStoredActionsPoolUI();
        SubmitTurn();
    }

    private IEnumerator EnemyTurnRoutine()
    {
        _turnRegistry.ResetVolatile();

        if (enemyTurnIntroDelayAfterPlayerDamageSeconds > 0f)
            yield return new WaitForSeconds(enemyTurnIntroDelayAfterPlayerDamageSeconds);

        if (enemyTurnIntroRoot != null)
        {
            ChangeState(CombatState.EnemyTurnIntro);
            yield return CoEnemyTurnIntroPresentation();
        }

        ChangeState(CombatState.EnemyTurn);
        yield return new WaitForSeconds(1.0f);
        if (activeEnemy != null && player != null)
        {
            var statusCtx = BuildStatusContext();
            activeEnemy.StatusEffects.TickBeforeEnemyTurn(statusCtx);
            if (CheckVictory()) yield break;

            EnemyActionSO action = activeEnemy.GetCurrentAction();
            if (action.damage > 0)
            {
                for (int i = 0; i < action.numberOfAttacks; i++)
                {
                    var boosted = action.damage + activeEnemy.StatusEffects.GetTotalPerDieAttackDamageBonus(statusCtx);
                    var damage = activeEnemy.StatusEffects.ModifyEnemyHitDamage(statusCtx, boosted);
                    if (activeEnemy.StatusEffects.CheckRedirectAttackToSelf(statusCtx)) activeEnemy.TakeDamage(damage);
                    else
                    {
                        var hadImmune = player.StatusEffects.GetStacks<ImmuneEffectSO>() > 0;
                        if (hadImmune)
                            damage = Mathf.Min(damage, 1);
                        player.TakeDamage(damage, PlayerDamageSource.EnemyPhysicalAttack);
                        if (hadImmune)
                            player.StatusEffects.ConsumeImmuneStackAfterHit(statusCtx);
                        var thornsRetaliate = player.StatusEffects.GetThornsRetaliateStacks();
                        if (thornsRetaliate > 0)
                            activeEnemy.TakeDamage(thornsRetaliate);
                    }
                    if (CheckVictory()) yield break;
                    if (CheckDefeat()) yield break;
                    if (action.numberOfAttacks > 1) yield return new WaitForSeconds(0.4f);
                }
            }
            if (action.armor > 0) activeEnemy.AddArmor(action.armor);

            if (action.actions != null && action.actions.Count > 0 && player != null)
            {
                var actionCtx = BuildEnemyActionContext(action);
                foreach (var gameAction in action.actions)
                {
                    if (gameAction == null) continue;
                    if (gameAction is FaceResolveModifierBase) continue;
                    gameAction.Execute(actionCtx);
                }
            }

            activeEnemy.StatusEffects.TickAfterEnemyTurn(statusCtx);
            player.StatusEffects.TickAfterEnemyTurn(statusCtx);
            if (CheckVictory()) yield break;
            activeEnemy.PrepareNextAction();
        }
        yield return new WaitForSeconds(1.0f);
        ResetTurn();
    }

    private IEnumerator CoEnemyTurnIntroPresentation()
    {
        if (enemyTurnIntroRoot == null)
            yield break;

        enemyTurnIntroRoot.SetActive(true);
        if (enemyTurnIntroDurationSeconds > 0f)
            yield return new WaitForSeconds(enemyTurnIntroDurationSeconds);
        enemyTurnIntroRoot.SetActive(false);
    }

    private bool CheckVictory()
    {
        if (activeEnemy.GetCurrentHealth() > 0) return false;
        // Enemy still at 0 HP on later ticks (turn start, thorns, etc.) must not re-fire victory UI.
        if (currentState == CombatState.Victory)
            return true;

        ChangeState(CombatState.Victory);

        VictoryRewardBuffer.PendingGold = 0;
        if (activeEnemy != null && activeEnemy.enemyData != null)
            VictoryRewardBuffer.PendingGold = Mathf.Max(0, activeEnemy.enemyData.goldReward);

        CombatEvents.OnPlayerVictory?.Invoke();
        return true;
    }

    private void CheatWinCombat()
    {
        if (activeEnemy == null) return;
        var hp = activeEnemy.GetCurrentHealth();
        if (hp <= 0)
        {
            CheckVictory();
            return;
        }

        activeEnemy.TakeTrueDamage(hp);
        CheckVictory();
    }
    private bool CheckDefeat() { if (player.GetCurrentHealth() <= 0) { ChangeState(CombatState.Defeat); CombatEvents.OnPlayerDefeat?.Invoke(); return true; } return false; }

    public void AddRollsRemaining(int amount)
    {
        if (amount < 0) return;
        rollsRemaining += amount;
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);
    }

    private void ResetTurn()
    {
        _sameTurnValueWatchers.Clear();
        _turnRegistry.ResetVolatile();
        channeledFaces.Clear();
        _pendingRerollGrants = 0;
        _rollBatchPipelineRunning = false;
        _pendingTopFaceByDieIndex = null;
        _pendingDieSourceByIndex = null;
        turnEndActions.Clear();
        overchargeBonus = 0;
        appliedMultiplier = 1;
        bustProtected = false;
        kineticShieldActive = false; kineticShieldBonus = 0; bonusDamageFromActions = 0; bonusArmorFromActions = 0;
        pendingPrecisionChoices.Clear();
        currentPower = 0; rollsRemaining = maxRolls; currentBatchIsFirstRollOfTurn = false;
        player.ResetArmor();
        var statusCtx = BuildStatusContext();
        // Player turn starts here.
        player.StatusEffects.TickTurnStart(statusCtx);
        NotifyAllStoredActionsPoolUI();
        CombatEvents.OnPowerChanged?.Invoke(0, maxPower);
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);
        CombatEvents.OnImmediateGameActionBarClear?.Invoke();
        CombatEvents.OnStoredActionsPoolRuntimeIconsClear?.Invoke();
        CombatEvents.OnPlayerTurnStarted?.Invoke();
        RelicActionRunner.RunPhase(this, RelicPhases.AfterEnemyTurnPlayerTurnStart);
        ChangeState(CombatState.WaitingForRoll);
    }

    private void ChangeState(CombatState newState) { currentState = newState; CombatEvents.OnStateChanged?.Invoke(newState); }
}