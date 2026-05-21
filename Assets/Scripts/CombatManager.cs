using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Enemies;

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
    [Tooltip("Optional. Shown after the delay above; stays up during the enemy turn and fades out via Canvas Group when actions finish.")]
    [SerializeField] private GameObject enemyTurnIntroRoot;
    [Tooltip("Required when Enemy Turn Intro Root is assigned. Typically on the same GameObject as the root.")]
    [SerializeField] private CanvasGroup enemyTurnIntroCanvasGroup;
    [SerializeField, Min(0f)] private float enemyTurnIntroFadeInSeconds = 0.2f;
    [SerializeField, Min(0f)] private float enemyTurnIntroFadeOutSeconds = 0.35f;

    [Header("Enemy turn intent")]
    [Tooltip("Runs each physical hit, armor gain, and game action as separate steps with delay and intent-row scale pulse. If unassigned, uses legacy execution (0.4s only between multi-hits).")]
    [SerializeField] private EnemyTurnIntentSequencePlayer enemyTurnIntentSequence;

    [Tooltip("After physical damage (+ thorns) to the enemy, wait this long before the first turn-start status damage tick (e.g. burn). Also waits this long between each subsequent status tick when multiple effects use OnTurnStart. Stack decay still runs once after all ticks, same as instant TickTurnStart.")]
    [SerializeField, Min(0f)] private float delaySecondsBetweenPhysicalAndEachEnemyStatusTick = 0f;

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
    [Header("Roll Platform Glow")]
    [Tooltip("Optional. Platform renderer using a material with _SelfLitIntensity.")]
    [SerializeField] private Renderer rollPlatformRenderer;
    [Tooltip("How long the platform glow takes to reach target intensity while dice are rolling.")]
    [SerializeField, Min(0f)] private float rollPlatformGlowRiseDurationSeconds = 0.35f;
    [Tooltip("Wait this long after roll starts before beginning glow rise.")]
    [SerializeField, Min(0f)] private float rollPlatformGlowStartDelaySeconds = 0f;
    [Tooltip("Target _SelfLitIntensity value during rolling.")]
    [SerializeField, Range(0f, 1f)] private float rollPlatformGlowTargetIntensity = 1f;
    [Tooltip("Hard wait from roll start before fading platform glow out.")]
    [SerializeField, Min(0f)] private float rollPlatformGlowFadeOutDelaySeconds = 0.25f;
    [Tooltip("How long the platform glow takes to fade down to 0 after the delay.")]
    [SerializeField, Min(0f)] private float rollPlatformGlowFadeOutDurationSeconds = 0.35f;

    private CombatState currentState;
    private List<DieAssetSO> selectedDice = new List<DieAssetSO>();

    private int currentPower;
    private int maxPower;
    private int _enemyMaxPowerReductionThisCombat;
    private int _enemyMaxPowerMinimumFloor = 1;
    private int overchargeBonus;
    private int appliedMultiplier;
    private int bonusDamageFromActions;
    private int bonusArmorFromActions;
    private int _playerArmorAtNextTurnStart;
    private bool bustProtected;
    private bool _warnedMissingEnemyIntentSequence;
    private bool _skipPowerOrbFlightForNextSubmitTurn;
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

    BurnEffectSO _burnOnPlayerArmorLostFromEnemyDef;
    int _burnStacksPerArmorLostFromEnemyPhysical;
    private readonly List<ValueBasedRollWatcherEntry> _sameTurnValueWatchers = new List<ValueBasedRollWatcherEntry>();
    private readonly List<ValueBasedRollWatcherEntry> _entireCombatValueWatchers = new List<ValueBasedRollWatcherEntry>();
    private List<DieAssetSO> _pendingBatchDiceAssets;
    private readonly Dictionary<DieAssetSO, int> _gemNoPowerOnMatchChargesRemainingByDie = new Dictionary<DieAssetSO, int>();
    private readonly Dictionary<DieAssetSO, int> _gemExtraRollGrantsThisTurnByDie = new Dictionary<DieAssetSO, int>();
    /// <summary>Per die: how many times <see cref="TryScheduleGemBatchRandomRerollsSkipPower"/> succeeded this roll batch (cleared when a new batch starts).</summary>
    private readonly Dictionary<DieAssetSO, int> _gemBonusRollChainActivationsByDieThisBatch = new Dictionary<DieAssetSO, int>();
    private readonly HashSet<int> _gemScheduledBatchRerollIndices = new HashSet<int>();
    private readonly HashSet<int> _noPowerOnNextGatherCommit = new HashSet<int>();
    private readonly HashSet<int> _gemBatchRerollIndicesInFlight = new HashSet<int>();
    private int _rollBatchId;
    /// <summary>Increments once per settled die (any batch). Used for face-registered value watchers so later dice in the same roll batch can match.</summary>
    private int _faceResolveSequence;
    private bool _echoSkipsPowerThisBatch;
    private Coroutine _rollPlatformGlowRoutine;
    private Coroutine _rollPlatformFadeRoutine;
    private MaterialPropertyBlock _rollPlatformGlowMpb;
    private bool _rollPlatformGlowHasOriginal;
    private float _rollPlatformGlowOriginalIntensity;
    private float _rollPlatformCurrentIntensity;

    private struct PendingAfterPhysicalApplyStatus
    {
        public ApplyStatusEffectAction Action;
        public FaceResult SourceFace;
    }

    private readonly List<PendingAfterPhysicalApplyStatus> _pendingAfterPhysicalApplyStatuses = new List<PendingAfterPhysicalApplyStatus>();
    private bool _afterPhysicalDeferredStatusPhaseCompleted;

    /// <summary>Volatile turn blackboard (physical/armor/burn totals, Brute Force, Supernova).</summary>
    public TurnRegistry TurnRegistry => _turnRegistry;

    /// <summary>Increments once per player roll command (batch). Used by <see cref="AddValueBasedOnRollDuration.SameTurn"/> / <see cref="AddValueBasedOnRollDuration.EntireCombat"/> watchers.</summary>
    public int CurrentRollBatchId => _rollBatchId;

    // Updated summation logic to pull from FaceResult properties
    public int GetPendingAttack() => channeledFaces.Sum(f => f.TotalDamageContribution) + bonusDamageFromActions;
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
            Add(PoolRowKey.FromDieType(DieType.Damage), face.TotalDamageContribution);
            Add(PoolRowKey.FromDieType(DieType.Armor), face.Armor);
            Add(PoolRowKey.FromDieType(DieType.Curse), face.TotalSelfDamageContribution);

            if (face.ActionPoolContributions == null) continue;
            foreach (var extra in face.ActionPoolContributions)
            {
                if (extra.VisualFlyoutOnly) continue;
                Add(extra.PoolKey, extra.Amount);
            }
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

    /// <summary>
    /// Invokes and clears <see cref="turnEndActions"/> (heal, cleanse, max HP grant, gems).
    /// Deferred face <see cref="ExecuteDeferredTurnEndActionsForSubmitTurn"/> can enqueue after the first drain when
    /// <paramref name="beforePlayerPhysicalDamage"/> is false — call again after that pass.
    /// </summary>
    private void DrainQueuedTurnEndActions(GameActionContext ctx)
    {
        if (turnEndActions == null || turnEndActions.Count == 0)
            return;

        for (var guard = 0; guard < 8 && turnEndActions.Count > 0; guard++)
        {
            var batch = new List<Action<GameActionContext>>(turnEndActions);
            turnEndActions.Clear();
            for (var i = 0; i < batch.Count; i++)
                batch[i]?.Invoke(ctx);
        }
    }
    public bool IsResolvingFirstRollOfTurn() => currentBatchIsFirstRollOfTurn;
    public int GetRollsRemaining() => rollsRemaining;
    public int GetCurrentPower() => currentPower;
    public int GetMaxPower() => maxPower;
    public CombatState GetCombatState() => currentState;

    /// <summary>
    /// Pre-roll odds that the selected dice land on Perfect Cast or Cast Overload (uniform face odds, status face-value mods, relic perfect windows).
    /// </summary>
    public bool TryComputeRollCastOdds(IReadOnlyList<DieAssetSO> diceToRoll, out float perfectPercent, out float bustPercent)
    {
        perfectPercent = 0f;
        bustPercent = 0f;
        if (currentState != CombatState.WaitingForRoll || diceToRoll == null || diceToRoll.Count == 0 || player == null)
            return false;

        var statusCtx = BuildStatusContext();
        var echoSkips = player.StatusEffects.WillEchoSkipPowerOnNextRollBatch();
        var perfectAtMaxMinusOne = maxPower > 1 &&
                                   RelicActionRunner.QueryBoolOr(RelicPhases.QueryPerfectAtMaxMinusOne, this);
        var perfectAtMaxPlusOne = RelicActionRunner.QueryBoolOr(RelicPhases.QueryPerfectAtMaxPlusOne, this);
        int ModifyFaceValue(int value) => player.StatusEffects.ModifyFaceValue(statusCtx, value);

        var result = RollCastOddsCalculator.Compute(
            currentPower,
            maxPower,
            perfectAtMaxMinusOne,
            perfectAtMaxPlusOne,
            echoSkips,
            diceToRoll,
            ModifyFaceValue);
        perfectPercent = result.PerfectPercent;
        bustPercent = result.BustPercent;
        return true;
    }
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

    /// <summary>
    /// Next player turn starts with this much armor instead of 0 (see <see cref="StartNextTurnWithArmorAction"/>).
    /// Multiple schedules in one turn use the highest value.
    /// </summary>
    public void SchedulePlayerArmorAtNextTurnStart(int amount)
    {
        if (amount <= 0)
            return;
        _playerArmorAtNextTurnStart = Mathf.Max(_playerArmorAtNextTurnStart, amount);
    }

    /// <summary>
    /// Enemy-intent debuff: reduce player's max power for this combat only.
    /// Final max power = max(floor, computedBase - totalReduction).
    /// </summary>
    public void ApplyEnemyMaxPowerReductionForCombat(int reductionAmount, int minimumAllowedMaxPower)
    {
        if (reductionAmount <= 0)
            return;

        _enemyMaxPowerReductionThisCombat += reductionAmount;
        _enemyMaxPowerMinimumFloor = Mathf.Max(_enemyMaxPowerMinimumFloor, Mathf.Max(1, minimumAllowedMaxPower));
        CalculateMaxPower();
    }

    /// <summary>Run after enemy HP changes outside normal submit resolution (e.g. instant burn proc from a face action).</summary>
    public void TryResolveVictoryAfterDirectEnemyDamage() => CheckVictory();

    /// <summary>
    /// For the upcoming enemy turn: each point of player armor lost to <see cref="PlayerDamageSource.EnemyPhysicalAttack"/>
    /// applies <paramref name="stacksPerArmorPoint"/> burn (using <paramref name="burnDefinition"/>) to the enemy. Cleared next player turn.
    /// </summary>
    public void RegisterBurnOnPlayerArmorLostFromEnemyPhysical(BurnEffectSO burnDefinition, int stacksPerArmorPoint)
    {
        if (burnDefinition == null)
        {
            Debug.LogError("CombatManager.RegisterBurnOnPlayerArmorLostFromEnemyPhysical: burnDefinition is required.");
            return;
        }

        if (burnDefinition.target != StatusEffectTarget.Enemy)
        {
            Debug.LogError("CombatManager.RegisterBurnOnPlayerArmorLostFromEnemyPhysical: burnDefinition must target Enemy.");
            return;
        }

        if (stacksPerArmorPoint <= 0)
        {
            Debug.LogError("CombatManager.RegisterBurnOnPlayerArmorLostFromEnemyPhysical: stacksPerArmorPoint must be positive.");
            return;
        }

        if (_burnOnPlayerArmorLostFromEnemyDef != null && _burnOnPlayerArmorLostFromEnemyDef != burnDefinition)
            Debug.LogWarning("CombatManager: burn-on-armor-lost already used a different Burn asset; switching to the latest registration.");

        _burnOnPlayerArmorLostFromEnemyDef = burnDefinition;
        _burnStacksPerArmorLostFromEnemyPhysical += stacksPerArmorPoint;
    }

    void HandlePlayerArmorLostToEnemyPhysicalAttack(int armorLost)
    {
        if (armorLost <= 0 || _burnStacksPerArmorLostFromEnemyPhysical <= 0 || _burnOnPlayerArmorLostFromEnemyDef == null) return;
        if (player == null || activeEnemy == null) return;
        if (currentState == CombatState.Victory || currentState == CombatState.Defeat) return;

        var applyStacks = armorLost * _burnStacksPerArmorLostFromEnemyPhysical;
        if (_burnOnPlayerArmorLostFromEnemyDef.target == StatusEffectTarget.Enemy)
            applyStacks += player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        if (applyStacks <= 0) return;

        var ctx = BuildStatusContextForEffects();
        activeEnemy.StatusEffects.ApplyStatus(_burnOnPlayerArmorLostFromEnemyDef, applyStacks, ctx);
        _turnRegistry.RecordBurnApplied(applyStacks);

        if (GameActionDebug.Enabled)
            Debug.Log($"[BurnOnArmorLost] +{applyStacks} burn ({armorLost} armor lost × {_burnStacksPerArmorLostFromEnemyPhysical})");
    }

    /// <summary>Player power meter (Resonance): additive, can exceed max toward bust.</summary>
    public void AddResonancePower(int delta)
    {
        if (delta == 0) return;
        currentPower += delta;
        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
    }

    /// <summary>
    /// Gem: pick up to <paramref name="otherDiceCount"/> random dice later in this batch's gather order, rethrow them for free,
    /// and their next committed face contributes 0 power. Each <paramref name="triggerDie"/> can proc at most
    /// <paramref name="maxActivationsPerRoll"/> times per roll batch. Rerolls run during gather before those indices commit.
    /// </summary>
    public bool TryScheduleGemBatchRandomRerollsSkipPower(
        DieAssetSO triggerDie,
        int gatherIndex,
        int otherDiceCount,
        int maxActivationsPerRoll = 3)
    {
        if (triggerDie == null || spawner == null) return false;
        if (otherDiceCount <= 0) return false;
        if (expectedDiceCount <= 0 || gatherIndex < 0 || gatherIndex >= expectedDiceCount) return false;

        var cap = maxActivationsPerRoll > 0 ? maxActivationsPerRoll : 3;
        _gemBonusRollChainActivationsByDieThisBatch.TryGetValue(triggerDie, out var used);
        if (used >= cap) return false;

        var candidates = new List<int>();
        for (var j = gatherIndex + 1; j < expectedDiceCount; j++)
        {
            if (!_gemBatchRerollIndicesInFlight.Contains(j))
                candidates.Add(j);
        }

        if (candidates.Count == 0) return false;

        _gemBonusRollChainActivationsByDieThisBatch[triggerDie] = used + 1;

        var pick = Mathf.Min(otherDiceCount, candidates.Count);
        for (var s = 0; s < pick; s++)
        {
            var r = UnityEngine.Random.Range(s, candidates.Count);
            (candidates[s], candidates[r]) = (candidates[r], candidates[s]);
        }

        for (var k = 0; k < pick; k++)
        {
            var idx = candidates[k];
            _gemScheduledBatchRerollIndices.Add(idx);
            _gemBatchRerollIndicesInFlight.Add(idx);
        }

        return true;
    }

    /// <summary>GrantExtraRollsThisTurn gem: each die can grant at most <paramref name="maxGrantsPerTurnPerDie"/> times per player turn.</summary>
    public bool TryApplyGemExtraRollsPerDie(DieAssetSO die, int grantRolls, int maxGrantsPerTurnPerDie = 2)
    {
        if (die == null || grantRolls <= 0) return false;
        var cap = Mathf.Max(1, maxGrantsPerTurnPerDie);
        _gemExtraRollGrantsThisTurnByDie.TryGetValue(die, out var used);
        if (used >= cap) return false;

        _gemExtraRollGrantsThisTurnByDie[die] = used + 1;
        AddRollsRemaining(grantRolls);
        return true;
    }

    /// <summary>Consumes one "no power on match" charge for this die when available.</summary>
    public bool TryConsumeGemNoPowerOnMatchCharge(DieAssetSO die)
    {
        if (die == null) return false;
        if (!_gemNoPowerOnMatchChargesRemainingByDie.TryGetValue(die, out var left) || left <= 0)
            return false;
        _gemNoPowerOnMatchChargesRemainingByDie[die] = left - 1;
        return true;
    }

    public GameActionContext BuildGameActionContextForFace(FaceResult triggeringFace)
    {
        return new GameActionContext
        {
            CombatManager = this,
            Player = player,
            Enemy = activeEnemy,
            ChanneledFaces = channeledFaces,
            TriggeringFace = triggeringFace,
            PlayerData = playerData,
            RelicRuntime = _relicRuntime,
            CurrentPower = currentPower,
            MaxPower = maxPower
        };
    }

    private PlayerDataSO playerData;
    private bool _mapCombatBootstrapped;
    private bool _mapPendingEnemyCoroutineRunning;

    private void Awake()
    {
        if (spawner == null || player == null || activeEnemy == null)
            Debug.LogError("CombatManager: Missing references!");
        if (gameIconIndex != null)
            GameIconCatalog.Register(gameIconIndex);
    }

    private void Start() => TryBootstrapMapCombatIfNeeded();

    /// <summary>
    /// Map handoff may arrive after this scene is additively loaded, or while roots are disabled (coroutines stop).
    /// OnEnable + Start both call this so the first moment pending enemy exists we bootstrap once.
    /// </summary>
    private void TryBootstrapMapCombatIfNeeded()
    {
        if (_mapCombatBootstrapped)
            return;

        if (RunManager.Instance != null && RunManager.Instance.UseMapBasedRun)
        {
            if (RunEncounterBuffer.PendingEnemyType == null)
            {
                if (!_mapPendingEnemyCoroutineRunning)
                {
                    _mapPendingEnemyCoroutineRunning = true;
                    StartCoroutine(CoWaitForPendingMapEnemyThenInit());
                }

                return;
            }

            if (InitializeEnemy())
            {
                InitializeCombat();
                _mapCombatBootstrapped = true;
            }

            return;
        }

        if (InitializeEnemy())
        {
            InitializeCombat();
            _mapCombatBootstrapped = true;
        }
    }

    private IEnumerator CoWaitForPendingMapEnemyThenInit()
    {
        const int maxFrames = 3600;
        var frames = 0;
        while (RunEncounterBuffer.PendingEnemyType == null)
        {
            if (++frames > maxFrames || RunManager.Instance == null || !RunManager.Instance.UseMapBasedRun)
            {
                Debug.LogError(
                    "CombatManager: Map-based run — RunEncounterBuffer never received a pending enemy (e.g. fight scene started without map handoff).");
                _mapPendingEnemyCoroutineRunning = false;
                yield break;
            }

            yield return null;
        }

        if (InitializeEnemy())
        {
            InitializeCombat();
            _mapCombatBootstrapped = true;
        }

        _mapPendingEnemyCoroutineRunning = false;
    }

    /// <returns>False when combat should not start (e.g. map handoff missing).</returns>
    private bool InitializeEnemy()
    {
        if (RunEncounterBuffer.TryConsumePendingEnemy(out var mapEnemy))
        {
            activeEnemy.Initialize(mapEnemy);
            BindStatusBars();
            return true;
        }

        if (RunManager.Instance != null && RunManager.Instance.UseMapBasedRun)
        {
            Debug.LogError("CombatManager: Map-based run expected a pending enemy from RunEncounterBuffer.");
            return false;
        }

        if (RunManager.Instance != null)
        {
            var room = RunManager.Instance.CurrentRoom;
            if (room == null || room.roomType != RoomType.Combat) return false;
            activeEnemy.Initialize(room.enemyType);
            BindStatusBars();
            return true;
        }
        if (activeEnemy.enemyData != null) activeEnemy.Initialize(activeEnemy.enemyData);
        BindStatusBars();
        return true;
    }

    private void OnEnable()
    {
        TryBootstrapMapCombatIfNeeded();
        CombatEvents.OnDieToggled += HandleDieToggle;
        CombatEvents.OnRollCommand += ExecuteBatchRoll;
        CombatEvents.OnBustResolved += ResolveBust;
        CombatEvents.OnEndTurnPressed += ManualEndTurn;
        CombatEvents.OnCheatWinPressed += CheatWinCombat;
        CombatEvents.OnCheatPerfectStrikePressed += ForcePerfectStrikeCheat;
        CombatEvents.OnPlayerArmorLostToEnemyPhysicalAttack += HandlePlayerArmorLostToEnemyPhysicalAttack;
        CombatEvents.OnPlayerHealthDepleted += HandlePlayerHealthDepleted;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerHealthDepleted -= HandlePlayerHealthDepleted;

        // Do not capture before InitializeCombat — additive preload hides the fight scene while PlayerStatus is still at Awake defaults (1/1).
        if (_mapCombatBootstrapped && player != null && RunManager.Instance != null && RunManager.Instance.UseMapBasedRun
            && player.GetCurrentHealth() > 0)
            RunManager.Instance.CaptureRunVitalityFromPlayer(player);

        // Fight scene stays loaded with roots toggled off between map visits; must re-bootstrap on next activation.
        _mapCombatBootstrapped = false;
        _mapPendingEnemyCoroutineRunning = false;
        CombatEvents.OnDieToggled -= HandleDieToggle;
        CombatEvents.OnRollCommand -= ExecuteBatchRoll;
        CombatEvents.OnBustResolved -= ResolveBust;
        CombatEvents.OnEndTurnPressed -= ManualEndTurn;
        CombatEvents.OnCheatWinPressed -= CheatWinCombat;
        CombatEvents.OnCheatPerfectStrikePressed -= ForcePerfectStrikeCheat;
        CombatEvents.OnPlayerArmorLostToEnemyPhysicalAttack -= HandlePlayerArmorLostToEnemyPhysicalAttack;
    }

    void HandlePlayerHealthDepleted() => CheckDefeat();

    private void InitializeCombat()
    {
        FightScenePresentationCleanup.Apply(gameObject.scene);

        if (PlayerDataContainer.Instance == null) return;
        playerData = PlayerDataContainer.Instance.RuntimeData;
        ApplyTestStartingFaces();
        if (player != null && playerData != null)
        {
            if (RunManager.Instance != null && RunManager.Instance.UseMapBasedRun)
                RunManager.Instance.ApplyRunVitalityToPlayerIfAny(player);
            else
                player.ApplyStartingHealthFromPlayerData(playerData);
        }
        _relicRuntime = new RelicRuntimeState();
        ResetStats();
        RelicActionRunner.RunPhase(this, RelicPhases.CombatStart);
        if (RunManager.Instance != null && RunManager.Instance.UseMapBasedRun)
            RunManager.Instance.TryApplyPermanentStrengthStacksAtCombatStart(this, player, activeEnemy);
        ChangeState(CombatState.WaitingForRoll);
        CombatEvents.OnCombatSessionInitialized?.Invoke();
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
        _pendingAfterPhysicalApplyStatuses.Clear();
        _afterPhysicalDeferredStatusPhaseCompleted = false;
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
        _playerArmorAtNextTurnStart = 0;
        _burnOnPlayerArmorLostFromEnemyDef = null;
        _burnStacksPerArmorLostFromEnemyPhysical = 0;
        pendingPrecisionChoices.Clear();
        currentPower = 0;
        _enemyMaxPowerReductionThisCombat = 0;
        _enemyMaxPowerMinimumFloor = 1;
        maxRolls = playerData.maxRollsPerTurn + RelicActionRunner.QueryIntSum(RelicPhases.QueryMaxRollsBonus, this);
        if (maxRolls < 1)
            maxRolls = 1;
        rollsRemaining = maxRolls;
        currentBatchIsFirstRollOfTurn = false;
        CalculateMaxPower();
        NotifyAllStoredActionsPoolUI();
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);

        _gemNoPowerOnMatchChargesRemainingByDie.Clear();
        if (playerData?.currentDeck != null)
        {
            foreach (var die in playerData.currentDeck)
            {
                if (die == null) continue;
                var charges = die.SumGemNoPowerOnMatchCharges();
                if (charges > 0)
                    _gemNoPowerOnMatchChargesRemainingByDie[die] = charges;
            }
        }

        _gemBonusRollChainActivationsByDieThisBatch.Clear();
        _gemScheduledBatchRerollIndices.Clear();
        _noPowerOnNextGatherCommit.Clear();
        _gemBatchRerollIndicesInFlight.Clear();
        _gemExtraRollGrantsThisTurnByDie.Clear();

        BindStatusBars();

        CombatEvents.OnImmediateGameActionBarClear?.Invoke();
        CombatEvents.OnStoredActionsPoolRuntimeIconsClear?.Invoke();
    }

    private void BindStatusBars()
    {
        if (playerStatusBar != null && player != null && player.StatusEffects != null)
        {
            playerStatusBar.Bind(player.StatusEffects);
            playerStatusBar.BindPlayerCombatBarBuffs(_turnRegistry);
        }

        if (enemyStatusBar != null && activeEnemy != null && activeEnemy.StatusEffects != null)
        {
            enemyStatusBar.Bind(activeEnemy.StatusEffects);
            enemyStatusBar.BindEnemyStartingBuffs(activeEnemy);
        }

        if (player != null && player.StatusEffects != null)
            player.StatusEffects.BindBattleContext(this, player);
        if (activeEnemy != null && activeEnemy.StatusEffects != null)
            activeEnemy.StatusEffects.BindBattleContext(this, player);
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

        maxPower = PlayerMaxPowerForRun.Compute(playerData);
        maxPower = Mathf.Max(_enemyMaxPowerMinimumFloor, maxPower - _enemyMaxPowerReductionThisCombat);
        if (currentPower > maxPower)
            currentPower = maxPower;

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
        StartRollPlatformGlow();
        _rollBatchId++;
        _gemBonusRollChainActivationsByDieThisBatch.Clear();
        _gemScheduledBatchRerollIndices.Clear();
        _noPowerOnNextGatherCommit.Clear();
        _gemBatchRerollIndicesInFlight.Clear();
        _pendingRerollGrants = 0;
        _echoSkipsPowerThisBatch = player != null &&
                                   player.StatusEffects.TryConsumeEchoPowerSkipForNextRollBatch(BuildStatusContext());
        expectedDiceCount = selectedDice.Count;
        _pendingTopFaceByDieIndex = new DieFaceSO[expectedDiceCount];
        _pendingDieSourceByIndex = new Transform[expectedDiceCount];
        pendingRollVisualSequences = 0;
        _rollBatchPipelineRunning = false;
        _pendingBatchDiceAssets = new List<DieAssetSO>(selectedDice);
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
            yield return CoDrainGemScheduledRerolls();

            var f = _pendingTopFaceByDieIndex[i];
            var t = _pendingDieSourceByIndex[i];
            if (f == null)
            {
                Debug.LogError($"CombatManager: Missing pending face at index {i} after special-effects phase.");
                continue;
            }

            var dieAsset = _pendingBatchDiceAssets != null && i < _pendingBatchDiceAssets.Count
                ? _pendingBatchDiceAssets[i]
                : null;
            var skipPower = _noPowerOnNextGatherCommit.Remove(i);
            CommitResolvedRoll(f, t, dieAsset, i, skipPower);
            yield return CoDrainGemScheduledRerolls();
        }

        QueueAddPowerChoicesAfterBatchGather(batchGatherStart, channeledFaces.Count);

        _rollBatchPipelineRunning = false;
        _pendingTopFaceByDieIndex = null;
        _pendingDieSourceByIndex = null;
        _pendingBatchDiceAssets = null;

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

    private IEnumerator CoDrainGemScheduledRerolls()
    {
        while (_gemScheduledBatchRerollIndices.Count > 0)
        {
            var batch = new List<int>(_gemScheduledBatchRerollIndices);
            _gemScheduledBatchRerollIndices.Clear();

            foreach (var j in batch)
            {
                if (j < 0 || j >= expectedDiceCount) continue;
                if (_pendingTopFaceByDieIndex == null || j >= _pendingTopFaceByDieIndex.Length) continue;

                var go = spawner != null ? spawner.GetActiveDieGameObject(j) : null;
                if (go == null)
                {
                    Debug.LogError($"CombatManager: Gem batch reroll — no active die GameObject for batch index {j}.");
                    _gemBatchRerollIndicesInFlight.Remove(j);
                    continue;
                }

                _pendingTopFaceByDieIndex[j] = null;
                _pendingDieSourceByIndex[j] = null;
                _noPowerOnNextGatherCommit.Add(j);
                spawner.RerollDiePhysics(go);
            }

            yield return new WaitUntil(() =>
            {
                if (_pendingTopFaceByDieIndex == null) return true;
                foreach (var j in batch)
                {
                    if (j < 0 || j >= _pendingTopFaceByDieIndex.Length) continue;
                    if (_pendingTopFaceByDieIndex[j] == null) return false;
                }

                return true;
            });
        }
    }

    private void CommitResolvedRoll(DieFaceSO face, Transform dieWorldSource, DieAssetSO sourceDieAsset, int batchGatherIndex, bool skipPowerContribution)
    {
        _gemBatchRerollIndicesInFlight.Remove(batchGatherIndex);
        _faceResolveSequence++;

        bool kineticArmorThisRoll = kineticShieldActive;
        if (kineticArmorThisRoll) kineticShieldBonus++;

        var statusCtx = BuildStatusContext();
        var modifiedValue = player.StatusEffects.ModifyFaceValue(statusCtx, face.value);
        var rolledDamage = face.damage;
        if (rolledDamage > 0 && face.type != DieType.Curse)
            rolledDamage += player.StatusEffects.GetTotalPerDieAttackDamageBonus(statusCtx);

        var result = new FaceResult
        {
            Face = face,
            Value = modifiedValue,
            Type = face.type,
            Damage = face.type == DieType.Curse ? 0 : rolledDamage,
            DamageAttackTimes = face.type == DieType.Damage ? Mathf.Max(1, face.damageAttackTimes) : 1,
            Armor = face.type == DieType.Curse ? 0 : face.armor,
            SelfDamage = face.type == DieType.Curse ? Mathf.Max(0, face.selfDamage) : 0,
        };
        if (face.actions != null)
        {
            foreach (var a in face.actions)
                if (a != null)
                    result.Actions.Add(a);
        }

        result.DieSource = dieWorldSource;
        result.PowerContributionThisResolve =
            (_echoSkipsPowerThisBatch || skipPowerContribution) ? 0 : modifiedValue;
        result.KineticShieldBonusContribution = kineticArmorThisRoll ? 1 : 0;

        if (face.type == DieType.Fire && _turnRegistry.PendingNextFireRollDoubleEnemyBurn)
        {
            result.DoubleEnemyBurnStacksThisResolve = true;
            _turnRegistry.PendingNextFireRollDoubleEnemyBurn = false;
            _turnRegistry.SetPlayerBarBuff(ActionVisualId.PrimeNextFireRollDoubleEnemyBurn, active: false);
        }

        ApplyQueuedNextRollMultiplier(result, _turnRegistry);

        if (face.actions != null)
        {
            foreach (var a in face.actions)
            {
                if (a is FaceResolveModifierBase mod && mod.ActivateImmediately)
                    mod.Modify(face, result, this, _turnRegistry);
            }
        }

        var relicModifyCtx = BuildRelicContext(result);
        relicModifyCtx.RelicPhase = RelicPhases.ModifyFaceResult;
        relicModifyCtx.CurrentPower = currentPower;
        relicModifyCtx.MaxPower = maxPower;
        RelicActionRunner.ExecuteAllRelics(relicModifyCtx);

        if (sourceDieAsset != null)
            GemCombatResolver.ApplySocketedGems(sourceDieAsset, result, this, batchGatherIndex);

        ApplyValueBasedRollWatchersArmorDamage(result);

        if (face.actions != null)
        {
            foreach (var a in face.actions)
            {
                if (a is FaceResolveModifierBase mod && !mod.ActivateImmediately)
                    mod.Modify(face, result, this, _turnRegistry);
            }
        }

        _turnRegistry.RecordResolvedFace(result);

        PopulateActionPoolContributions(result);

        channeledFaces.Add(result);
        if (!_echoSkipsPowerThisBatch)
        {
            currentPower += result.PowerContributionThisResolve;
            if (result.PowerContributionThisResolve > 0)
                ProgressionEventBridge.NotifyAccumulatedPower(result.PowerContributionThisResolve);
        }

        ProgressionEventBridge.NotifyExactRoll(modifiedValue);

        var relicAfterPowerCtx = BuildRelicContext(result);
        relicAfterPowerCtx.RelicPhase = RelicPhases.AfterPowerChangedFromRoll;
        relicAfterPowerCtx.CurrentPower = currentPower;
        relicAfterPowerCtx.MaxPower = maxPower;
        RelicActionRunner.ExecuteAllRelics(relicAfterPowerCtx);

        ApplyValueBasedRollWatchersBurn(result);

        if (result.Actions.Count > 0)
        {
            var context = BuildContext(result);
            List<Sprite> immediateIcons = null;
            foreach (var a in result.Actions)
            {
                if (a == null) continue;
                if (a is FaceResolveModifierBase) continue;
                if (!a.ActivateImmediately) continue;
                if (a is RerollDieAction) continue;
                if (a is AddPowerAction) continue;
                if (a is ApplyStatusEffectAction applyLate &&
                    applyLate.StatusEffectDefinition != null &&
                    !applyLate.StatusEffectDefinition.ActivateBeforePlayerPhysicalDamage)
                {
                    _pendingAfterPhysicalApplyStatuses.Add(new PendingAfterPhysicalApplyStatus
                    {
                        Action = applyLate,
                        SourceFace = result
                    });
                    continue;
                }

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
        if (activeEnemy != null)
            activeEnemy.HandlePlayerFaceResolved(result, this);

        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
        NotifyStoredActionsPoolUpdated();
        if (FaceHasAnyDeferredExecutableAction(result))
            PushDeferredPoolIconHints(result);

        if (dieWorldSource != null)
        {
            var lines = BuildRollVisualLines(result, kineticArmorThisRoll);
            if (lines.Count > 0 && CombatEvents.OnDiceRollVisualFeedback != null)
            {
                var activateAfterRegularDice =
                    sourceDieAsset != null &&
                    sourceDieAsset.GetSocketedGems().Any(g =>
                        g?.effects != null &&
                        g.effects.Any(e => e != null && e.kind == GemEffectKind.RandomBatchRerollOtherDiceNoPower));
                pendingRollVisualSequences++;
                var payload = new DiceRollVisualPayload
                {
                    WorldAnchor = dieWorldSource.position,
                    DieTransform = dieWorldSource,
                    Lines = lines,
                    ActivateAfterRegularDice = activateAfterRegularDice,
                    NeedsDelayedStoredPoolResync = lines.Any(l => l.IsVisualFlyoutOnly)
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

        if (pendingRollVisualSequences == 0)
            CombatEvents.OnStoredActionsPoolIconsFullResync?.Invoke(BuildStoredActionsPool());
    }

    private void PopulateActionPoolContributions(FaceResult result)
    {
        if (result.Actions == null || player == null) return;
        foreach (var a in result.Actions)
        {
            if (a is FaceResolveModifierWithIcon modWithIcon)
            {
                modWithIcon.AppendFlyoutContributionIfAny(result);
                continue;
            }

            if (a is FaceResolveModifierBase) continue;
            if (a is ApplyStatusEffectAction apply)
                apply.AppendPoolContributionIfAny(result, player, apply.ActivateImmediately);
            if (a is MaxHpAction maxHp)
                maxHp.AppendPoolContributionIfAny(result);
            if (a is AddValueBasedOnRollAction valueBonus)
                valueBonus.AppendPoolContributionIfAny(result, player);
            if (a is ThornsAction thorns)
                thorns.AppendPoolContributionIfAny(result, thorns.ActivateImmediately);
            if (a is HealAction heal)
                heal.AppendPoolContributionIfAny(result);
            if (a is StartNextTurnWithArmorAction startNextTurnArmor)
                startNextTurnArmor.AppendPoolContributionIfAny(result);
        }
    }

    /// <param name="fromRelicCombatStart">When true, the first player roll batch of the fight is skipped (watchers start from batch 2).</param>
    public void RegisterValueBasedRollWatcher(
        FaceValueMatchSet requiredFaceValues,
        RollBonusType bonusType,
        int amount,
        BurnEffectSO burnDefinition,
        AddValueBasedOnRollDuration duration,
        bool fromRelicCombatStart = false)
    {
        if (duration == AddValueBasedOnRollDuration.SameRoll)
        {
            Debug.LogError("CombatManager.RegisterValueBasedRollWatcher: SameRoll does not use watchers.");
            return;
        }

        if (!ValidateValueBasedRollWatcherRegistration(amount, bonusType, burnDefinition, matchAnyFaceValue: true))
            return;

        var firstBatch = fromRelicCombatStart
            ? Mathf.Max(2, _rollBatchId + 2)
            : Mathf.Max(1, _rollBatchId + 1);

        var entry = CreateValueBasedRollWatcherEntry(matchAnyFaceValue: true, bonusType, amount, burnDefinition);
        entry.FirstEligibleBatchId = firstBatch;
        entry.FirstEligibleResolveSequence = 0;

        AddValueBasedRollWatcherEntry(entry, duration);
    }

    /// <summary>Registers a watcher when a die face with <see cref="AddValueBasedOnRollAction"/> resolves (Same Turn / Entire Combat).</summary>
    public void RegisterValueBasedRollWatcherAnyDieFromDieResolution(
        int amount,
        RollBonusType bonusType,
        BurnEffectSO burnDefinition,
        AddValueBasedOnRollDuration duration)
    {
        if (!ValidateValueBasedRollWatcherRegistration(amount, bonusType, burnDefinition, matchAnyFaceValue: true))
            return;

        var entry = CreateValueBasedRollWatcherEntry(matchAnyFaceValue: true, bonusType, amount, burnDefinition);
        entry.FirstEligibleBatchId = 0;
        entry.FirstEligibleResolveSequence = _faceResolveSequence + 1;

        AddValueBasedRollWatcherEntry(entry, duration);
    }

    static bool ValidateValueBasedRollWatcherRegistration(
        int amount,
        RollBonusType bonusType,
        BurnEffectSO burnDefinition,
        bool matchAnyFaceValue)
    {
        if (amount <= 0)
            return false;
        if (bonusType == RollBonusType.Burn && burnDefinition == null)
        {
            Debug.LogError("CombatManager: burnDefinition required when Bonus Type is Burn.");
            return false;
        }

        if (!matchAnyFaceValue)
            Debug.LogError("CombatManager: value-based roll watchers must use any-die matching.");

        return true;
    }

    static ValueBasedRollWatcherEntry CreateValueBasedRollWatcherEntry(
        bool matchAnyFaceValue,
        RollBonusType bonusType,
        int amount,
        BurnEffectSO burnDefinition)
    {
        return new ValueBasedRollWatcherEntry
        {
            MatchAnyFaceValue = matchAnyFaceValue,
            BonusType = bonusType,
            Amount = amount,
            BurnDefinition = burnDefinition
        };
    }

    void AddValueBasedRollWatcherEntry(ValueBasedRollWatcherEntry entry, AddValueBasedOnRollDuration duration)
    {
        if (duration == AddValueBasedOnRollDuration.EntireCombat)
            _entireCombatValueWatchers.Add(entry);
        else
            _sameTurnValueWatchers.Add(entry);
    }

    private void CollectValueWatcherRegistrationsFromFace(FaceResult face)
    {
        if (face?.Actions == null) return;
        foreach (var a in face.Actions)
        {
            if (a is FaceResolveModifierBase) continue;
            if (a is AddValueBasedOnRollAction valueBonus)
                valueBonus.RegisterWatcherIfNeeded(this, face);
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
        if (!FaceValueMatchSet.MatchesAny(result.Value, w.RequiredFaceValues, w.MatchAnyFaceValue))
            return;
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
        if (!FaceValueMatchSet.MatchesAny(result.Value, w.RequiredFaceValues, w.MatchAnyFaceValue))
            return;
        if (w.BonusType != RollBonusType.Burn || w.Amount <= 0) return;

        AddValueBasedOnRollAction.ApplyBurnToEnemyFromContext(ctx, w.Amount, w.BurnDefinition);
        AddValueBasedOnRollAction.TryAppendBurnPoolLineForWatcher(
            result, player, w.RequiredFaceValues, w.MatchAnyFaceValue, w.Amount, w.BurnDefinition);
    }

    private static bool FaceHasAnyDeferredExecutableAction(FaceResult result)
    {
        if (result?.Actions == null) return false;
        foreach (var a in result.Actions)
        {
            if (a is FaceResolveModifierBase) continue;
            if (a is RerollDieAction) continue;
            if (a is AddPowerAction) continue;
            if (a == null) continue;
            if (!a.ActivateImmediately) return true;
        }

        return false;
    }

    private static void PushDeferredPoolIconHints(FaceResult result)
    {

        void Hint(PoolRowKey key, int amt, Sprite icon)
        {
            if (amt > 0 && icon != null)
                CombatEvents.OnRuntimePoolIconForRow?.Invoke(key, icon);
        }

        Hint(PoolRowKey.FromDieType(DieType.Damage), result.TotalDamageContribution, GameIconCatalog.GetElementIcon(DieType.Damage));
        Hint(PoolRowKey.FromDieType(DieType.Armor), result.Armor, GameIconCatalog.GetElementIcon(DieType.Armor));
        Hint(PoolRowKey.FromDieType(DieType.Curse), result.TotalSelfDamageContribution, GameIconCatalog.GetElementIcon(DieType.Curse));

        if (result.ActionPoolContributions == null) return;
        foreach (var extra in result.ActionPoolContributions)
        {
            Hint(extra.PoolKey, extra.Amount, extra.Icon);
            var bg = extra.PoolRowBackground != null
                ? extra.PoolRowBackground
                : GameIconCatalog.TryGetPoolRowBackground(extra.PoolKey);
            if (extra.Amount > 0 && bg != null)
                CombatEvents.OnRuntimePoolRowBackgroundForRow?.Invoke(extra.PoolKey, bg);
        }
    }

    private static List<RollOutcomeVisualLine> BuildRollVisualLines(FaceResult result, bool kineticArmorThisRoll)
    {
        var lines = new List<RollOutcomeVisualLine>();

        void AddLine(PoolRowKey key, int amt, Sprite icon)
        {
            if (amt <= 0) return;
            lines.Add(new RollOutcomeVisualLine { RowKey = key, Amount = amt, IconOverride = icon });
        }

        AddLine(PoolRowKey.FromDieType(DieType.Damage), result.TotalDamageContribution, GameIconCatalog.GetElementIcon(DieType.Damage));
        AddLine(PoolRowKey.FromDieType(DieType.Armor), result.Armor, GameIconCatalog.GetElementIcon(DieType.Armor));
        AddLine(PoolRowKey.FromDieType(DieType.Curse), result.TotalSelfDamageContribution, GameIconCatalog.GetElementIcon(DieType.Curse));

        if (result.ActionPoolContributions != null)
        {
            foreach (var extra in result.ActionPoolContributions)
            {
                if (extra.Amount <= 0) continue;
                var rowBg = extra.PoolRowBackground != null
                    ? extra.PoolRowBackground
                    : GameIconCatalog.TryGetPoolRowBackground(extra.PoolKey);
                lines.Add(new RollOutcomeVisualLine
                {
                    RowKey = extra.PoolKey,
                    Amount = extra.Amount,
                    IconOverride = extra.Icon,
                    BackgroundOverride = rowBg,
                    IsVisualFlyoutOnly = extra.VisualFlyoutOnly,
                    FlyToPlayerStatusBar = extra.FlyToPlayerStatusBar
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

    public GameActionContext BuildEnemyActionContext(EnemyActionSO sourceIntent)
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

    public GameActionContext BuildEnemyPassiveActionContext(FaceResult triggeringFace = null)
    {
        return new GameActionContext
        {
            CombatManager = this,
            Player = player,
            Enemy = activeEnemy,
            ChanneledFaces = channeledFaces,
            TriggeringFace = triggeringFace,
            PlayerData = playerData,
            RelicRuntime = _relicRuntime,
            CurrentPower = currentPower,
            MaxPower = maxPower
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

    /// <summary>One physical strike from the current enemy intent (buffs, immune cap, thorns).</summary>
    public void ApplySingleEnemyPhysicalHitFromIntent(EnemyActionSO action)
    {
        if (action == null || action.damage <= 0 || activeEnemy == null || player == null)
            return;

        var statusCtx = BuildStatusContext();
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
    }

    public void ApplyEnemyArmorFromIntent(EnemyActionSO action)
    {
        if (action != null && action.armor > 0 && activeEnemy != null)
            activeEnemy.AddArmor(action.armor);
    }

    public void ExecuteEnemyIntentGameActionAtIndex(EnemyActionSO action, int actionListIndex)
    {
        if (action?.actions == null || actionListIndex < 0 || actionListIndex >= action.actions.Count || player == null)
            return;

        var gameAction = action.actions[actionListIndex];
        if (gameAction == null || gameAction is FaceResolveModifierBase)
            return;

        var actionCtx = BuildEnemyActionContext(action);
        gameAction.Execute(actionCtx);
    }

    public bool EvaluateEnemyTurnCombatEnded()
    {
        if (CheckVictory()) return true;
        if (CheckDefeat()) return true;
        return false;
    }

    private void ProcessPrecisionQueue()
    {
        if (pendingPrecisionChoices.Count > 0)
        {
            var entry = pendingPrecisionChoices.Dequeue();
            precisionPanel.Show(entry.Amount, entry.Presentation, accepted =>
            {
                if (accepted)
                {
                    currentPower += entry.Amount;
                    CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
                    ProgressionEventBridge.NotifyPerfectCast();
                }
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
        var perfectAtMaxPlusOne = currentPower == maxPower + 1 &&
                                  RelicActionRunner.QueryBoolOr(RelicPhases.QueryPerfectAtMaxPlusOne, this);
        if (perfectAtMax || perfectAtMaxMinusOne || perfectAtMaxPlusOne)
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
                if (face.Type == DieType.Curse)
                    face.SelfDamage *= appliedMultiplier;
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
                ProgressionEventBridge.NotifyCastOverload();
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

    /// <summary>
    /// End Turn from UI must run the same perfect / bust pipeline as <see cref="ProcessPrecisionQueue"/> —
    /// otherwise <see cref="appliedMultiplier"/> never applies and turn-end heals (etc.) stay at ×1.
    /// </summary>
    private void ManualEndTurn()
    {
        if (currentState != CombatState.WaitingForRoll) return;

        var perfectAtMax = currentPower == maxPower;
        var perfectAtMaxMinusOne = maxPower > 1 && currentPower == maxPower - 1 &&
                                   RelicActionRunner.QueryBoolOr(RelicPhases.QueryPerfectAtMaxMinusOne, this);
        var perfectAtMaxPlusOne = currentPower == maxPower + 1 &&
                                  RelicActionRunner.QueryBoolOr(RelicPhases.QueryPerfectAtMaxPlusOne, this);
        if (perfectAtMax || perfectAtMaxMinusOne || perfectAtMaxPlusOne || currentPower > maxPower)
        {
            CheckBustStatus();
            return;
        }

        SubmitTurn();
    }

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

    private void ResolveBust()
    {
        foreach (var face in channeledFaces)
        {
            face.Damage = 0;
            face.Armor = 0;
            face.DamageAttackTimes = 0;
            face.PowerContributionThisResolve = 0;
            if (face.Actions != null)
                face.Actions.Clear();
            if (face.ActionPoolContributions == null) continue;
            for (var i = 0; i < face.ActionPoolContributions.Count; i++)
            {
                var c = face.ActionPoolContributions[i];
                c.Amount = 0;
                face.ActionPoolContributions[i] = c;
            }
        }

        bonusDamageFromActions = 0;
        bonusArmorFromActions = 0;
        _playerArmorAtNextTurnStart = 0;
        kineticShieldBonus = 0;
        turnEndActions.Clear();
        _pendingAfterPhysicalApplyStatuses.Clear();
        _afterPhysicalDeferredStatusPhaseCompleted = true;
        _turnRegistry.ResetVolatile();
        if (player?.StatusEffects != null)
            player.StatusEffects.RemoveStatus<NextTurnArmorEffectSO>(BuildStatusContext());

        NotifyAllStoredActionsPoolUI();
        _skipPowerOrbFlightForNextSubmitTurn = true;
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
                if (c.VisualFlyoutOnly) continue;
                if (c.PoolSourceAction == null && c.MaxHpPoolSource == null && !c.PerfectStrikeScales) continue;
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

    /// <summary>Final amount for one deferred gem row after pool scaling/bust edits.</summary>
    public int ResolveGemDeferredPoolAmount(int handleId)
    {
        if (handleId <= 0 || channeledFaces == null) return 0;
        foreach (var face in channeledFaces)
        {
            foreach (var c in face.ActionPoolContributions)
            {
                if (c.GemDeferredHandleId != handleId) continue;
                return Mathf.Max(0, c.Amount);
            }
        }

        return 0;
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
        if (spawner != null)
            spawner.ClearOldDice();

        _afterPhysicalDeferredStatusPhaseCompleted = false;
        ChangeState(CombatState.TurnEnd);
        var ctx = BuildContext();

        RelicActionRunner.RunPhase(this, RelicPhases.BeforeSubmitTurn);

        ExecuteDeferredTurnEndActionsForSubmitTurn(beforePlayerPhysicalDamage: true);

        DrainQueuedTurnEndActions(ctx);

        var statusCtx = BuildStatusContext();
        int pendingAttack = GetPendingAttack();
        pendingAttack += player.StatusEffects.GetTotalBonusAttack(statusCtx);
        pendingAttack = activeEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, pendingAttack);
        int pendingDefense = GetPendingDefense();

        bool enemyDamageLine = HasAnyPendingEnemyDamage() || _turnRegistry.BurnAppliedThisTurn > 0;
        bool skipOrbFlightThisSubmit = _skipPowerOrbFlightForNextSubmitTurn;
        _skipPowerOrbFlightForNextSubmitTurn = false;

        bool usePowerOrb = powerOrbVisual != null && activeEnemy != null && player != null &&
                           (currentPower > 0 || pendingDefense > 0 || enemyDamageLine) &&
                           !skipOrbFlightThisSubmit;

        if (!usePowerOrb)
        {
            if (skipOrbFlightThisSubmit && powerOrbVisual != null)
                powerOrbVisual.NotifyBustTurnResolutionWithoutOrbFlight();
            ApplyPlayerTurnCombatResults(pendingAttack, pendingDefense);
        }
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

    private bool HasAnyPendingEnemyDamage()
    {
        if (bonusDamageFromActions > 0)
            return true;
        if (player != null)
        {
            var bonus = player.StatusEffects.GetTotalBonusAttack(BuildStatusContext());
            if (bonus > 0)
                return true;
        }

        for (var i = 0; i < channeledFaces.Count; i++)
        {
            var face = channeledFaces[i];
            if (face != null && face.Damage > 0)
                return true;
        }

        return false;
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
        {
            player.AddArmor(pendingDefense);
            ProgressionEventBridge.NotifyDamageBlocked(pendingDefense);
        }

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
            continueCombat = RunPlayerPhysicalResolution(pendingAttack);
        }

        IEnumerator flight = powerOrbVisual.RunFlightToWorldAnchor(
            orbAnchor, allowZeroCombatPower, forceStartingVisibleScale, OnOrbImpact);
        while (flight.MoveNext())
            yield return flight.Current;

        if (!attackResolved)
            OnOrbImpact();
        if (!continueCombat) yield break;

        yield return StartCoroutine(CoResolveEnemyOpeningAndStartEnemyTurn());
    }

    /// <summary>Physical damage + thorns when applicable, then deferred / queued status applies that must run after that damage.</summary>
    private bool RunPlayerPhysicalResolution(int pendingAttack)
    {
        if (pendingAttack > 0 && !ApplyPendingPlayerAttackFromTurn(pendingAttack))
            return false;
        TryExecuteDeferredStatusAppliesAfterPlayerPhysical();
        return true;
    }

    private void ExecuteDeferredTurnEndActionsForSubmitTurn(bool beforePlayerPhysicalDamage)
    {
        foreach (var face in channeledFaces)
        {
            if (face.Actions == null || face.Actions.Count == 0) continue;
            var faceCtx = BuildContext(face);
            faceCtx.PendingApplyStackOverrides = BuildPendingApplyStackOverrides(face);
            foreach (var a in face.Actions)
            {
                if (a is FaceResolveModifierBase) continue;
                if (a is AddPowerAction) continue;
                if (a == null) continue;
                if (a.ActivateImmediately) continue;

                if (a is ApplyStatusEffectAction apply)
                {
                    if (apply.StatusEffectDefinition == null)
                    {
                        Debug.LogError("CombatManager: ApplyStatusEffectAction has no status assigned on a deferred face.");
                        continue;
                    }

                    if (apply.StatusEffectDefinition.ActivateBeforePlayerPhysicalDamage != beforePlayerPhysicalDamage)
                        continue;
                }
                else if (!beforePlayerPhysicalDamage)
                    continue;

                a.Execute(faceCtx);
            }
        }
    }

    private void TryExecuteDeferredStatusAppliesAfterPlayerPhysical()
    {
        if (_afterPhysicalDeferredStatusPhaseCompleted) return;
        if (currentState == CombatState.Victory || currentState == CombatState.Defeat)
            return;

        _afterPhysicalDeferredStatusPhaseCompleted = true;

        for (var i = 0; i < _pendingAfterPhysicalApplyStatuses.Count; i++)
        {
            var p = _pendingAfterPhysicalApplyStatuses[i];
            var faceCtx = BuildContext(p.SourceFace);
            faceCtx.PendingApplyStackOverrides = BuildPendingApplyStackOverrides(p.SourceFace);
            p.Action.Execute(faceCtx);
        }

        _pendingAfterPhysicalApplyStatuses.Clear();

        ExecuteDeferredTurnEndActionsForSubmitTurn(beforePlayerPhysicalDamage: false);

        DrainQueuedTurnEndActions(BuildContext());

        NotifyStoredActionsPoolUpdated();
    }

    /// <summary>Enemy damage and thorns from the player turn; does not apply armor or start the enemy turn.</summary>
    /// <returns>False if defeat or victory ended combat.</returns>
    private bool ApplyCurseSelfDamageFromChanneledFaces()
    {
        if (player == null || channeledFaces == null)
            return true;

        var total = 0;
        for (var i = 0; i < channeledFaces.Count; i++)
        {
            var f = channeledFaces[i];
            if (f == null || f.Type != DieType.Curse)
                continue;
            total += Mathf.Max(0, f.SelfDamage);
        }

        if (total <= 0)
            return true;

        player.TakeDamage(total, PlayerDamageSource.CurseFace);
        return !CheckDefeat();
    }

    private bool ApplyPendingPlayerAttackFromTurn(int pendingAttack)
    {
        if (!ApplyCurseSelfDamageFromChanneledFaces())
            return false;

        var statusCtx = BuildStatusContext();
        var playerBonusAttack = player.StatusEffects.GetTotalBonusAttack(statusCtx);
        var totalPhysical = Mathf.Max(0, bonusDamageFromActions + playerBonusAttack);
        var totalFire = 0;
        var totalIce = 0;
        var totalNature = 0;

        for (var i = 0; i < channeledFaces.Count; i++)
        {
            var face = channeledFaces[i];
            if (face == null || face.Damage <= 0)
                continue;

            switch (face.Type)
            {
                case DieType.Damage:
                    totalPhysical += face.TotalDamageContribution;
                    break;
                case DieType.Fire:
                    totalFire += face.Damage;
                    break;
                case DieType.Ice:
                    totalIce += face.Damage;
                    break;
                case DieType.Nature:
                    totalNature += face.Damage;
                    break;
            }
        }

        totalPhysical = activeEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, totalPhysical);
        totalFire = activeEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, totalFire);
        totalIce = activeEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, totalIce);
        totalNature = activeEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, totalNature);

        if (totalPhysical > 0)
        {
            activeEnemy.TakeDamage(totalPhysical, DieType.Damage, EnemyDamagePresentationKind.Physical);
            ProgressionEventBridge.NotifyPhysicalDamageDealt(totalPhysical);
        }

        if (totalFire > 0)
        {
            activeEnemy.TakeDamage(totalFire, DieType.Fire, EnemyDamagePresentationKind.Physical);
            ProgressionEventBridge.NotifyFireDamageDealt(totalFire);
        }
        if (totalIce > 0)
            activeEnemy.TakeDamage(totalIce, DieType.Ice, EnemyDamagePresentationKind.Physical);
        if (totalNature > 0)
            activeEnemy.TakeDamage(totalNature, DieType.Nature, EnemyDamagePresentationKind.Physical);

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
    /// After all player-turn damage to the enemy: stepped <see cref="StatusEffectManager.TickTurnStartStepped"/> (optional delays),
    /// then <see cref="EnemyController.ResetArmor"/> before the enemy-turn coroutine.
    /// </summary>
    private IEnumerator CoResolveEnemyOpeningAndStartEnemyTurn()
    {
        if (activeEnemy != null && player != null)
        {
            var openingCtx = BuildStatusContext();
            yield return StartCoroutine(activeEnemy.StatusEffects.TickTurnStartStepped(
                openingCtx,
                delaySecondsBetweenPhysicalAndEachEnemyStatusTick,
                delaySecondsBetweenPhysicalAndEachEnemyStatusTick));
            if (CheckVictory()) yield break;
            activeEnemy.ResetArmor();
        }

        StartCoroutine(EnemyTurnRoutine());
    }

    private void ApplyPlayerTurnCombatResults(int pendingAttack, int pendingDefense)
    {
        StartCoroutine(CoApplyPlayerTurnCombatResults(pendingAttack, pendingDefense));
    }

    private IEnumerator CoApplyPlayerTurnCombatResults(int pendingAttack, int pendingDefense)
    {
        if (pendingDefense > 0 && player != null)
        {
            player.AddArmor(pendingDefense);
            ProgressionEventBridge.NotifyDamageBlocked(pendingDefense);
        }

        if (!RunPlayerPhysicalResolution(pendingAttack)) yield break;

        yield return StartCoroutine(CoResolveEnemyOpeningAndStartEnemyTurn());
    }

    private IEnumerator CoFinishJackpotAfterPresentation(int multiplier, Dictionary<PoolRowKey, int> poolsBefore, Dictionary<PoolRowKey, int> poolsAfter)
    {
        // Unity does not reliably run a nested IEnumerator with "yield return routine()"; must use StartCoroutine.
        yield return StartCoroutine(jackpotPresentation.Run(multiplier, poolsBefore, poolsAfter));
        NotifyAllStoredActionsPoolUI();
        SubmitTurn();
    }

    private IEnumerator CoExecuteEnemyTurnIntentLegacy(EnemyActionSO action)
    {
        if (action.damage > 0)
        {
            for (var i = 0; i < action.numberOfAttacks; i++)
            {
                ApplySingleEnemyPhysicalHitFromIntent(action);
                if (CheckVictory()) yield break;
                if (CheckDefeat()) yield break;
                if (action.numberOfAttacks > 1) yield return new WaitForSeconds(0.4f);
            }
        }

        ApplyEnemyArmorFromIntent(action);

        if (action.actions != null && action.actions.Count > 0 && player != null)
        {
            var actionCtx = BuildEnemyActionContext(action);
            foreach (var gameAction in action.actions)
            {
                if (gameAction == null) continue;
                if (gameAction is FaceResolveModifierBase) continue;
                gameAction.Execute(actionCtx);
            }

            if (CheckDefeat())
                yield break;
        }
    }

    private IEnumerator EnemyTurnRoutine()
    {
        _turnRegistry.ResetVolatile();

        if (enemyTurnIntroDelayAfterPlayerDamageSeconds > 0f)
            yield return new WaitForSeconds(enemyTurnIntroDelayAfterPlayerDamageSeconds);

        var enemyTurnIntroIsUp = false;
        if (enemyTurnIntroRoot != null)
        {
            ChangeState(CombatState.EnemyTurnIntro);
            yield return CoEnemyTurnIntroShow();
            enemyTurnIntroIsUp = true;
        }

        ChangeState(CombatState.EnemyTurn);
        yield return new WaitForSeconds(1.0f);
        if (activeEnemy != null && player != null)
        {
            var statusCtx = BuildStatusContext();
            activeEnemy.StatusEffects.TickBeforeEnemyTurn(statusCtx);
            if (CheckVictory())
            {
                yield return CoTeardownEnemyTurnIntroIfShown(enemyTurnIntroIsUp);
                yield break;
            }

            EnemyActionSO action = activeEnemy.GetCurrentAction();
            yield return activeEnemy.CoPresentEnemyTurnActionIntro(action);

            if (enemyTurnIntentSequence != null)
                yield return enemyTurnIntentSequence.CoExecuteIntent(activeEnemy, action, this);
            else
            {
                if (!_warnedMissingEnemyIntentSequence)
                {
                    _warnedMissingEnemyIntentSequence = true;
                    Debug.LogWarning(
                        $"{nameof(CombatManager)} on '{name}': assign {nameof(enemyTurnIntentSequence)} for stepped enemy intent + UI pulse; using legacy enemy turn execution.",
                        this);
                }

                yield return CoExecuteEnemyTurnIntentLegacy(action);
            }

            yield return activeEnemy.CoPresentEnemyTurnActionOutro();

            activeEnemy.StatusEffects.TickAfterEnemyTurn(statusCtx);
            player.StatusEffects.TickAfterEnemyTurn(statusCtx);
            if (CheckVictory() || CheckDefeat())
            {
                yield return CoTeardownEnemyTurnIntroIfShown(enemyTurnIntroIsUp);
                yield break;
            }

            activeEnemy.PrepareNextAction();
        }

        yield return CoTeardownEnemyTurnIntroIfShown(enemyTurnIntroIsUp);

        if (currentState == CombatState.Victory || currentState == CombatState.Defeat)
            yield break;

        yield return new WaitForSeconds(1.0f);
        ResetTurn();
    }

    private IEnumerator CoEnemyTurnIntroShow()
    {
        if (enemyTurnIntroRoot == null)
            yield break;

        if (enemyTurnIntroCanvasGroup == null)
            throw new InvalidOperationException(
                $"{nameof(CombatManager)} on '{name}': {nameof(enemyTurnIntroRoot)} is assigned but {nameof(enemyTurnIntroCanvasGroup)} is not. Add a Canvas Group to the intro UI and assign it.");

        enemyTurnIntroCanvasGroup.alpha = 0f;
        enemyTurnIntroRoot.SetActive(true);

        if (enemyTurnIntroFadeInSeconds <= 0f)
        {
            enemyTurnIntroCanvasGroup.alpha = 1f;
            yield break;
        }

        var t = 0f;
        while (t < enemyTurnIntroFadeInSeconds)
        {
            t += Time.deltaTime;
            enemyTurnIntroCanvasGroup.alpha = Mathf.Clamp01(t / enemyTurnIntroFadeInSeconds);
            yield return null;
        }

        enemyTurnIntroCanvasGroup.alpha = 1f;
    }

    private IEnumerator CoTeardownEnemyTurnIntroIfShown(bool introWasRaised)
    {
        if (!introWasRaised)
            yield break;

        yield return CoFadeOutEnemyTurnIntroThenDisable();
    }

    private IEnumerator CoFadeOutEnemyTurnIntroThenDisable()
    {
        if (enemyTurnIntroRoot == null || !enemyTurnIntroRoot.activeSelf)
            yield break;

        if (enemyTurnIntroCanvasGroup == null)
        {
            enemyTurnIntroRoot.SetActive(false);
            yield break;
        }

        if (enemyTurnIntroFadeOutSeconds <= 0f)
        {
            enemyTurnIntroCanvasGroup.alpha = 0f;
            enemyTurnIntroRoot.SetActive(false);
            yield break;
        }

        var startAlpha = enemyTurnIntroCanvasGroup.alpha;
        var t = 0f;
        while (t < enemyTurnIntroFadeOutSeconds)
        {
            t += Time.deltaTime;
            var k = Mathf.Clamp01(t / enemyTurnIntroFadeOutSeconds);
            enemyTurnIntroCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
            yield return null;
        }

        enemyTurnIntroCanvasGroup.alpha = 0f;
        enemyTurnIntroRoot.SetActive(false);
    }

    private bool CheckVictory()
    {
        if (activeEnemy.GetCurrentHealth() > 0) return false;
        // Enemy still at 0 HP on later ticks (turn start, thorns, etc.) must not re-fire victory UI.
        if (currentState == CombatState.Victory)
            return true;

        ChangeState(CombatState.Victory);

        SimulationSpeedController.ApplyRealtimeGlobally();

        VictoryRewardBuffer.PendingGold = 0;
        if (activeEnemy != null && activeEnemy.enemyData != null)
        {
            VictoryRewardBuffer.PendingGold = activeEnemy.enemyData.RollGoldReward();
            EnemyBonusRewardResolver.RollAndGrant(activeEnemy.enemyData);
        }

        RelicActionRunner.RunPhase(this, RelicPhases.OnCombatVictory);
        if (activeEnemy != null && activeEnemy.enemyData != null)
            ProgressionEventBridge.NotifyCombatVictory(activeEnemy.enemyData);
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
    private bool CheckDefeat()
    {
        if (currentState == CombatState.Defeat)
            return true;
        if (player == null || player.GetCurrentHealth() > 0)
            return false;

        ChangeState(CombatState.Defeat);
        CombatEvents.OnPlayerDefeat?.Invoke();
        return true;
    }

    public void AddRollsRemaining(int amount)
    {
        if (amount < 0) return;
        rollsRemaining += amount;
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);
    }

    private void ApplyPlayerTurnStartArmor()
    {
        if (player == null)
            return;

        if (_playerArmorAtNextTurnStart > 0)
        {
            player.SetArmor(_playerArmorAtNextTurnStart);
            _playerArmorAtNextTurnStart = 0;
            return;
        }

        player.ResetArmor();
    }

    private void ResetTurn()
    {
        EndRollPlatformGlow(forceImmediateReset: true);
        _sameTurnValueWatchers.Clear();
        _turnRegistry.ResetVolatile();
        channeledFaces.Clear();
        _pendingAfterPhysicalApplyStatuses.Clear();
        _afterPhysicalDeferredStatusPhaseCompleted = false;
        _pendingRerollGrants = 0;
        _rollBatchPipelineRunning = false;
        _pendingTopFaceByDieIndex = null;
        _pendingDieSourceByIndex = null;
        turnEndActions.Clear();
        overchargeBonus = 0;
        appliedMultiplier = 1;
        bustProtected = false;
        kineticShieldActive = false; kineticShieldBonus = 0; bonusDamageFromActions = 0; bonusArmorFromActions = 0;
        _burnOnPlayerArmorLostFromEnemyDef = null;
        _burnStacksPerArmorLostFromEnemyPhysical = 0;
        pendingPrecisionChoices.Clear();
        currentPower = 0; rollsRemaining = maxRolls; currentBatchIsFirstRollOfTurn = false;
        _gemBonusRollChainActivationsByDieThisBatch.Clear();
        _gemScheduledBatchRerollIndices.Clear();
        _noPowerOnNextGatherCommit.Clear();
        _gemBatchRerollIndicesInFlight.Clear();
        _gemExtraRollGrantsThisTurnByDie.Clear();
        var statusCtx = BuildStatusContext();
        // Enemy-applied player debuffs (Burn, Poison) tick before armor is cleared for the new turn.
        player.StatusEffects.TickTurnStartBeforePlayerArmorReset(statusCtx);
        if (CheckDefeat())
            return;

        ApplyPlayerTurnStartArmor();
        // Player turn starts here (Next Turn Armor, etc.).
        player.StatusEffects.TickTurnStart(statusCtx);
        if (CheckDefeat())
            return;

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

    private void StartRollPlatformGlow()
    {
        if (rollPlatformRenderer == null)
            return;
        if (!TryGetRollPlatformSelfLitOriginal(out var original))
            return;

        if (_rollPlatformGlowRoutine != null)
            StopCoroutine(_rollPlatformGlowRoutine);
        if (_rollPlatformFadeRoutine != null)
            StopCoroutine(_rollPlatformFadeRoutine);

        var target = Mathf.Clamp01(rollPlatformGlowTargetIntensity);
        var startDelay = Mathf.Max(0f, rollPlatformGlowStartDelaySeconds);
        var riseDuration = Mathf.Max(0f, rollPlatformGlowRiseDurationSeconds);
        var fadeDelay = Mathf.Max(0f, rollPlatformGlowFadeOutDelaySeconds);
        var fadeDuration = Mathf.Max(0f, rollPlatformGlowFadeOutDurationSeconds);
        _rollPlatformGlowRoutine = StartCoroutine(CoRiseRollPlatformGlow(original, target, startDelay, riseDuration));
        _rollPlatformFadeRoutine = StartCoroutine(CoFadeOutRollPlatformGlow(fadeDelay, fadeDuration));
    }

    private void EndRollPlatformGlow(bool forceImmediateReset = false)
    {
        if (_rollPlatformGlowRoutine != null)
        {
            StopCoroutine(_rollPlatformGlowRoutine);
            _rollPlatformGlowRoutine = null;
        }
        if (_rollPlatformFadeRoutine != null)
        {
            StopCoroutine(_rollPlatformFadeRoutine);
            _rollPlatformFadeRoutine = null;
        }

        if (rollPlatformRenderer == null || !_rollPlatformGlowHasOriginal)
            return;
        ApplyRollPlatformSelfLit(forceImmediateReset ? _rollPlatformGlowOriginalIntensity : 0f);
    }

    private IEnumerator CoRiseRollPlatformGlow(float from, float to, float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (duration <= 0f)
        {
            ApplyRollPlatformSelfLit(to);
            _rollPlatformGlowRoutine = null;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            ApplyRollPlatformSelfLit(Mathf.Lerp(from, to, t));
            yield return null;
        }

        ApplyRollPlatformSelfLit(to);
        _rollPlatformGlowRoutine = null;
    }

    private IEnumerator CoFadeOutRollPlatformGlow(float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var from = _rollPlatformCurrentIntensity;
        if (duration <= 0f)
        {
            ApplyRollPlatformSelfLit(0f);
            _rollPlatformFadeRoutine = null;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            ApplyRollPlatformSelfLit(Mathf.Lerp(from, 0f, t));
            yield return null;
        }

        ApplyRollPlatformSelfLit(0f);
        _rollPlatformFadeRoutine = null;
    }

    private bool TryGetRollPlatformSelfLitOriginal(out float intensity)
    {
        intensity = 0f;
        if (rollPlatformRenderer == null)
            return false;

        var shared = rollPlatformRenderer.sharedMaterial;
        if (shared == null || !shared.HasProperty("_SelfLitIntensity"))
            return false;

        if (!_rollPlatformGlowHasOriginal)
        {
            _rollPlatformGlowOriginalIntensity = shared.GetFloat("_SelfLitIntensity");
            _rollPlatformGlowHasOriginal = true;
            _rollPlatformCurrentIntensity = _rollPlatformGlowOriginalIntensity;
        }

        intensity = _rollPlatformGlowOriginalIntensity;
        return true;
    }

    private void ApplyRollPlatformSelfLit(float intensity)
    {
        if (rollPlatformRenderer == null)
            return;
        _rollPlatformCurrentIntensity = intensity;
        _rollPlatformGlowMpb ??= new MaterialPropertyBlock();
        rollPlatformRenderer.GetPropertyBlock(_rollPlatformGlowMpb);
        _rollPlatformGlowMpb.SetFloat("_SelfLitIntensity", intensity);
        rollPlatformRenderer.SetPropertyBlock(_rollPlatformGlowMpb);
    }
}