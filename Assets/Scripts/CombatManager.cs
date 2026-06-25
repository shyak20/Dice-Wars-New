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
    [Tooltip("The Main Enemy (roster slot 0). Always present; existing single-enemy logic treats this as the active enemy.")]
    public EnemyController activeEnemy;
    [Tooltip("Pre-placed extra enemy slots (max 2). Activated when the Main Enemy spawns adds on load or a spawn action runs. Leave disabled in the scene; the roster enables them as needed.")]
    [SerializeField] private List<EnemyController> additionalEnemySlots = new List<EnemyController>();

    [Header("Multi-enemy targeting")]
    [Tooltip("Optional. Drives drag-to-assign of enemy-targeted rolled outcomes onto a chosen enemy. Required for fights with more than one enemy.")]
    [SerializeField] private RollTargetAssignmentController targetAssignment;

    /// <summary>The drag-to-assign controller for multi-enemy fights (may be null in single-enemy setups).</summary>
    public RollTargetAssignmentController TargetAssignment => targetAssignment;

    /// <summary>Total enemies allowed on screen at once (Main + up to 2 adds).</summary>
    public const int MaxEnemies = 3;

    private readonly List<EnemyController> _activeEnemies = new List<EnemyController>();

    /// <summary>Roster slot 0 — the Main Enemy. Rewards and shared hooks source from this enemy.</summary>
    public EnemyController MainEnemy => activeEnemy;

    /// <summary>All currently-alive enemies in slot order (Main first).</summary>
    public IReadOnlyList<EnemyController> ActiveEnemies => _activeEnemies;

    /// <summary>True when more than one enemy is alive — enemy-targeted outcomes must be dragged onto a specific enemy.</summary>
    public bool IsMultiEnemy => _activeEnemies.Count > 1;

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
    [Tooltip("Optional. Shown after the delay above; stays visible through all enemy actions and fades out when they finish (animator outro on EnemyTurnIntentSequencePlayer when wired, otherwise Canvas Group fade).")]
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

    [Header("Die-to-die projectiles")]
    [Tooltip("Optional. Flies world-space projectiles from source dice to reroll targets before physics reroll.")]
    [SerializeField] private DieToDieProjectileController dieToDieProjectileController;
    [Tooltip("Reroll action icons fly from source die to each target (same prefab as gather flyouts).")]
    [SerializeField] private DiceRollOutcomeFlyoutController diceRollOutcomeFlyout;
    [Tooltip("Fallback projectile prefab when an action or gem entry does not specify one.")]
    [SerializeField] private GameObject defaultDieToDieProjectilePrefab;
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
    private int _combatMaxPowerBonus;
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
    private int pendingRollVisualRaiseSequences;
    private bool _postRaiseCombatGateOpen;
    private bool _skipFlyoutFlyPhaseThisBatch;

    /// <summary>True after bust/perfect/normal check completes for this batch; flyout FlyAB may proceed (unless bust skipped fly).</summary>
    public bool IsFlyoutFlyPhaseAllowed => _postRaiseCombatGateOpen;

    /// <summary>When true (Cast Overload), flyout raise stays visible but FlyAB to the Element Container is skipped.</summary>
    public bool SkipFlyoutFlyPhaseThisBatch => _skipFlyoutFlyPhaseThisBatch;

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

    private struct GemBatchRerollSchedule
    {
        public int TargetIndex;
        public int TriggerGatherIndex;
        public GameObject ProjectilePrefab;
    }

    private readonly List<GemBatchRerollSchedule> _gemScheduledBatchRerolls = new List<GemBatchRerollSchedule>();
    private readonly Queue<int> _pendingPlayerChoiceRerollSources = new Queue<int>();
    private readonly HashSet<int> _deferDissolveBatchIndicesForDieToDieLaunch = new HashSet<int>();
    private readonly HashSet<FaceResult> _facesAwaitingPostSubmitTriggeringReroll = new HashSet<FaceResult>();
    private readonly HashSet<FaceResult> _faceOutcomesSubmitted = new HashSet<FaceResult>();
    private readonly HashSet<int> _postSubmitRerollWaitingIndices = new HashSet<int>();
    private readonly Dictionary<int, DieFaceSO> _postSubmitRerollSettledFaces = new Dictionary<int, DieFaceSO>();
    private readonly Dictionary<int, DieAssetSO> _batchDieAssetByGatherIndex = new Dictionary<int, DieAssetSO>();
    /// <summary>Faces with post-submit <see cref="RerollDieAction"/> still resolving; bust / perfect cast waits until zero.</summary>
    private int _deferredTriggeringRerollFacesRemaining;
    private bool _batchHasRerollOtherDicePending;
    private bool _postBatchOtherDiceRerollCompleted = true;
    private bool _postBatchOtherDiceRerollStarted;
    private readonly HashSet<int> _batchRerollOtherTargetIndices = new HashSet<int>();
    private readonly HashSet<int> _batchRerollOtherKeeperIndices = new HashSet<int>();
    private readonly HashSet<FaceResult> _facesAwaitingPostBatchOtherDiceReroll = new HashSet<FaceResult>();
    private readonly HashSet<FaceResult> _pendingBatchSubmitForRerollOther = new HashSet<FaceResult>();
    private readonly HashSet<FaceResult> _postBatchSecondPassAwaitingSubmit = new HashSet<FaceResult>();
    private int _postBatchRollAgainInFlight;
    private readonly Dictionary<int, GameObject> _batchLiveDieByGatherIndex = new Dictionary<int, GameObject>();
    private readonly HashSet<int> _noPowerOnNextGatherCommit = new HashSet<int>();

    struct IncreaseOtherBatchGrant
    {
        public int KeeperBatchIndex;
        public IncreaseOtherElementsAction Action;
    }

    struct IncreaseOtherHitTarget
    {
        public FaceResult Face;
        public PoolRowKey RowKey;
    }

    private readonly List<IncreaseOtherBatchGrant> _batchIncreaseOtherGrants = new List<IncreaseOtherBatchGrant>();
    private bool _batchHasIncreaseOtherElementsPending;
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

        // Enemy-targeted rows (physical damage, enemy debuffs) live on each enemy's AssignedElementPool,
        // not the shared player Element Container (single- or multi-enemy).
        foreach (var face in channeledFaces)
        {
            if (!face.HasEnemyDamagePiece)
                Add(PoolRowKey.FromDieType(DieType.Damage), face.TotalDamageContribution);
            Add(PoolRowKey.FromDieType(DieType.Armor), face.Armor);
            Add(PoolRowKey.FromDieType(DieType.Curse), face.TotalSelfDamageContribution);

            if (face.ActionPoolContributions == null) continue;
            foreach (var extra in face.ActionPoolContributions)
            {
                if (extra.VisualFlyoutOnly) continue;
                if (IsEnemyTargetedPoolContribution(extra)) continue;
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
    public void AddCombatMaxPowerBonus(int amount)
    {
        if (amount <= 0)
            return;

        _combatMaxPowerBonus += amount;
        CalculateMaxPower();
    }

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
        int maxActivationsPerRoll = 3,
        GameObject projectilePrefab = null)
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
            _gemScheduledBatchRerolls.Add(new GemBatchRerollSchedule
            {
                TargetIndex = idx,
                TriggerGatherIndex = gatherIndex,
                ProjectilePrefab = projectilePrefab,
            });
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
    private Coroutine _deferredRosterSlotSyncRoutine;

    private void Awake()
    {
        if (spawner == null || player == null || activeEnemy == null)
            Debug.LogError("CombatManager: Missing references!");
        if (additionalEnemySlots != null && additionalEnemySlots.Count > MaxEnemies - 1)
            Debug.LogError(
                $"CombatManager: additionalEnemySlots has {additionalEnemySlots.Count} entries but at most {MaxEnemies - 1} add slots are supported.",
                this);
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
            SetupRoster(mapEnemy);
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
            SetupRoster(room.enemyType);
            return true;
        }
        SetupRoster(activeEnemy.enemyData);
        return true;
    }

    /// <summary>Builds the active roster from the Main Enemy plus its <see cref="EnemyTypeSO.spawnOnLoadAdds"/>, then binds status bars.</summary>
    /// <param name="mainType">The Main Enemy's type. The Main is activated <b>before</b> being initialized so its animator/sprite bind on an active GameObject.</param>
    private void SetupRoster(EnemyTypeSO mainType)
    {
        var resolvedMain = mainType ?? activeEnemy.enemyData;
        var onLoadAdds = CollectValidSpawnOnLoadAdds(resolvedMain);

        _activeEnemies.Clear();
        SyncAdditionalEnemySlotVisibility();

        activeEnemy.ActivateInRoster(this);
        if (resolvedMain != null)
            activeEnemy.Initialize(resolvedMain);
        _activeEnemies.Add(activeEnemy);

        var pendingAddInits = new List<(EnemyController slot, EnemyTypeSO type)>();
        for (var a = 0; a < onLoadAdds.Count; a++)
        {
            if (_activeEnemies.Count >= MaxEnemies)
                break;

            var addType = onLoadAdds[a];
            var slot = GetFreeEnemySlot();
            if (slot == null)
            {
                var mainName = resolvedMain != null ? resolvedMain.enemyName : activeEnemy.name;
                Debug.LogWarning(
                    $"CombatManager: '{mainName}' wants to spawn on-load add '{addType.enemyName}' but no free enemy slot is available (assign more to additionalEnemySlots).");
                break;
            }

            _activeEnemies.Add(slot);
            pendingAddInits.Add((slot, addType));
        }

        SyncAdditionalEnemySlotVisibility();
        for (var i = 0; i < pendingAddInits.Count; i++)
            pendingAddInits[i].slot.Initialize(pendingAddInits[i].type);

        BindStatusBars();
        ValidateMultiEnemyTargetingSetup();
        CombatEvents.OnEnemyRosterChanged?.Invoke(_activeEnemies);
        ScheduleDeferredRosterSlotVisibilitySync();
    }

    /// <summary>Non-null entries from <see cref="EnemyTypeSO.spawnOnLoadAdds"/> on the main enemy type, capped by roster size.</summary>
    private static List<EnemyTypeSO> CollectValidSpawnOnLoadAdds(EnemyTypeSO mainType)
    {
        var result = new List<EnemyTypeSO>();
        if (mainType?.spawnOnLoadAdds == null)
            return result;

        for (var i = 0; i < mainType.spawnOnLoadAdds.Count; i++)
        {
            if (result.Count >= MaxEnemies - 1)
                break;

            var addType = mainType.spawnOnLoadAdds[i];
            if (addType != null)
                result.Add(addType);
        }

        return result;
    }

    /// <summary>Warns (once per setup) when a multi-enemy fight is missing the targeting wiring needed for drag-to-assign.</summary>
    private void ValidateMultiEnemyTargetingSetup()
    {
        if (!IsMultiEnemy)
            return;

        if (targetAssignment == null)
            Debug.LogWarning(
                "CombatManager: multi-enemy fight but no RollTargetAssignmentController assigned — enemy-targeted outcomes will auto-assign to the Main Enemy instead of being draggable.");

        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            var enemy = _activeEnemies[i];
            if (enemy == null) continue;
            if (enemy.AssignedElementPool == null)
                Debug.LogWarning($"CombatManager: enemy '{enemy.name}' has no AssignedElementPool — assigned outcomes will not be shown under it.");
            if (enemy.DropTarget == null)
                Debug.LogWarning($"CombatManager: enemy '{enemy.name}' has no EnemyDropTarget — players cannot drop outcomes on it.");
        }
    }

    /// <summary>First configured extra slot not already in the active roster, or null when full.</summary>
    public EnemyController GetFreeEnemySlot()
    {
        if (additionalEnemySlots == null) return null;
        foreach (var slot in additionalEnemySlots)
        {
            if (slot == null || slot == activeEnemy) continue;
            if (_activeEnemies.Contains(slot)) continue;
            return slot;
        }

        return null;
    }

    /// <summary>Mid-combat spawn (e.g. from an enemy intent action). Returns the spawned enemy, or null when the roster is full.</summary>
    public EnemyController SpawnEnemy(EnemyTypeSO type)
    {
        if (type == null)
        {
            Debug.LogError("CombatManager.SpawnEnemy: type is null.");
            return null;
        }

        if (_activeEnemies.Count >= MaxEnemies)
            return null;

        var slot = GetFreeEnemySlot();
        if (slot == null)
            return null;

        _activeEnemies.Add(slot);
        SyncAdditionalEnemySlotVisibility();
        slot.Initialize(type);
        BindStatusBars();
        ValidateMultiEnemyTargetingSetup();
        CombatEvents.OnEnemySpawned?.Invoke(slot);
        CombatEvents.OnEnemyRosterChanged?.Invoke(_activeEnemies);
        return slot;
    }

    /// <summary>
    /// Turns on each configured <see cref="additionalEnemySlots"/> entry that is in <see cref="_activeEnemies"/> and turns off the rest.
    /// On-load adds from <see cref="EnemyTypeSO.spawnOnLoadAdds"/> are added to the roster in <see cref="SetupRoster"/> before this runs.
    /// </summary>
    private void SyncAdditionalEnemySlotVisibility()
    {
        if (additionalEnemySlots == null)
            return;

        for (var i = 0; i < additionalEnemySlots.Count; i++)
        {
            var slot = additionalEnemySlots[i];
            if (slot == null)
            {
                Debug.LogError($"CombatManager: additionalEnemySlots[{i}] is null — assign the pre-placed add enemy in FightScene.", this);
                continue;
            }

            if (slot == activeEnemy)
            {
                Debug.LogError("CombatManager: additionalEnemySlots must not include the Main Enemy (activeEnemy).", slot);
                continue;
            }

            var shouldShow = _activeEnemies.Contains(slot);
            if (shouldShow)
            {
                // Re-activate when the flag is unset OR the GameObject was turned off externally (e.g. RunManager
                // restoring fight-scene roots to their saved inactive state after SetupRoster ran).
                if (!slot.IsActiveInRoster || !slot.gameObject.activeSelf)
                    slot.ActivateInRoster(this);
            }
            else if (slot.IsActiveInRoster || (slot.gameObject.activeSelf && !slot.IsHideAfterDefeatPending))
            {
                slot.DeactivateFromRoster();
            }
        }
    }

    /// <summary>
    /// Re-applies slot visibility on the next frame. Required for map-run additive preload: <see cref="RunManager"/> restores
    /// each fight-scene root to its captured default active state after <see cref="SetupRoster"/> runs inside CombatManager
    /// OnEnable, which turns pre-placed add-enemy roots back off when they were saved inactive in the scene.
    /// </summary>
    private void ScheduleDeferredRosterSlotVisibilitySync()
    {
        if (!isActiveAndEnabled)
            return;

        if (_deferredRosterSlotSyncRoutine != null)
            StopCoroutine(_deferredRosterSlotSyncRoutine);
        _deferredRosterSlotSyncRoutine = StartCoroutine(CoDeferredRosterSlotVisibilitySync());
    }

    private IEnumerator CoDeferredRosterSlotVisibilitySync()
    {
        yield return null;
        _deferredRosterSlotSyncRoutine = null;
        SyncAdditionalEnemySlotVisibility();
    }

    /// <summary>Removes any enemies that have reached 0 HP from the active roster, firing presentation events once each.</summary>
    private void PruneDefeatedEnemies()
    {
        for (var i = _activeEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = _activeEnemies[i];
            if (enemy == null)
            {
                _activeEnemies.RemoveAt(i);
                continue;
            }

            if (enemy.IsAlive) continue;

            _activeEnemies.RemoveAt(i);
            CombatEvents.OnEnemyDefeated?.Invoke(enemy);
            if (enemy == activeEnemy)
                enemy.ClearAssignedPoolOnDefeat();
            else
            {
                var hideDelay = enemy.CombatPresentation != null
                    ? enemy.CombatPresentation.DefeatedHideDelaySeconds
                    : 0f;
                enemy.ScheduleDeactivateFromRoster(hideDelay);
            }
        }

        SyncAdditionalEnemySlotVisibility();

        if (_activeEnemies.Count > 0)
            CombatEvents.OnEnemyRosterChanged?.Invoke(_activeEnemies);
    }

    private bool AnyEnemyAlive()
    {
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            if (_activeEnemies[i] != null && _activeEnemies[i].IsAlive)
                return true;
        }

        return false;
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
        if (_deferredRosterSlotSyncRoutine != null)
        {
            StopCoroutine(_deferredRosterSlotSyncRoutine);
            _deferredRosterSlotSyncRoutine = null;
        }
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
            player.ApplyCharacterPortrait(playerData);
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
        ScheduleDeferredRosterSlotVisibilitySync();
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
        ClearPostSubmitTriggeringRerollState();
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
        _combatMaxPowerBonus = 0;
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
        _gemScheduledBatchRerolls.Clear();
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

        if (player != null && player.StatusEffects != null)
            player.StatusEffects.BindBattleContext(this, player);

        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            var enemy = _activeEnemies[i];
            if (enemy == null || enemy.StatusEffects == null) continue;

            // Each enemy uses its own status bar when present; the Main Enemy falls back to the shared CombatManager bar.
            var bar = enemy.OwnStatusBar != null
                ? enemy.OwnStatusBar
                : (enemy == activeEnemy ? enemyStatusBar : null);

            if (bar != null)
            {
                bar.Bind(enemy.StatusEffects);
                bar.BindEnemyStartingBuffs(enemy);
            }
            else if (enemy != activeEnemy)
            {
                Debug.LogWarning(
                    $"CombatManager: enemy '{enemy.name}' has no status bar assigned (EnemyController.ownStatusBar); its debuffs will not be shown.");
            }

            enemy.StatusEffects.BindBattleContext(this, player);
        }
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

        maxPower = PlayerMaxPowerForRun.Compute(playerData) + _combatMaxPowerBonus;
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
        _gemScheduledBatchRerolls.Clear();
        _deferDissolveBatchIndicesForDieToDieLaunch.Clear();
        _noPowerOnNextGatherCommit.Clear();
        _gemBatchRerollIndicesInFlight.Clear();
        _pendingRerollGrants = 0;
        ClearPostSubmitTriggeringRerollState();
        ClearBatchIncreaseOtherElementsState();
        _echoSkipsPowerThisBatch = player != null &&
                                   player.StatusEffects.TryConsumeEchoPowerSkipForNextRollBatch(BuildStatusContext());
        expectedDiceCount = selectedDice.Count;
        _pendingTopFaceByDieIndex = new DieFaceSO[expectedDiceCount];
        _pendingDieSourceByIndex = new Transform[expectedDiceCount];
        pendingRollVisualSequences = 0;
        pendingRollVisualRaiseSequences = 0;
        _postRaiseCombatGateOpen = false;
        _skipFlyoutFlyPhaseThisBatch = false;
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

        if (_postSubmitRerollWaitingIndices.Contains(batchIndex))
        {
            _postSubmitRerollSettledFaces[batchIndex] = face;
            _postSubmitRerollWaitingIndices.Remove(batchIndex);
            return;
        }

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

    private static int CountRerollGrantsOnFace(DieFaceSO face, RerollDieAction.RerollDieScope scope)
    {
        if (face?.actions == null) return 0;
        var n = 0;
        for (var i = 0; i < face.actions.Count; i++)
        {
            if (face.actions[i] is RerollDieAction reroll && reroll.Scope == scope)
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
        var playerChoiceRerolls = CountRerollGrantsFromAllPendingFaces(RerollDieAction.RerollDieScope.PlayerChoosesAnyDie);
        if (rerollDieSelection == null && playerChoiceRerolls > 0)
            Debug.LogError("CombatManager: Reroll Die on a face but rerollDieSelection is not assigned.");

        _pendingRerollGrants = CountRerollGrantsFromAllPendingFaces(RerollDieAction.RerollDieScope.PlayerChoosesAnyDie);
        BuildPendingPlayerChoiceRerollSourceQueue();
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

            var sourceIdx = _pendingPlayerChoiceRerollSources.Count > 0
                ? _pendingPlayerChoiceRerollSources.Dequeue()
                : -1;
            _pendingRerollGrants = Mathf.Max(0, _pendingRerollGrants - 1);

            if (!skipped && picked != null)
            {
                var idx = spawner.GetIndexOfActiveDie(picked);
                if (idx < 0)
                    Debug.LogWarning("CombatManager: Picked die is not in the active batch.");
                else
                {
                    if (sourceIdx < 0)
                        Debug.LogWarning("CombatManager: Player-choice reroll grant has no source die index.");

                    var sourceTransform = sourceIdx >= 0 ? GetBatchDieTransform(sourceIdx) : null;
                    var sourceFace = sourceIdx >= 0 && _pendingTopFaceByDieIndex != null && sourceIdx < _pendingTopFaceByDieIndex.Length
                        ? _pendingTopFaceByDieIndex[sourceIdx]
                        : null;
                    var launchIcon = ResolveDieToDieLaunchIconFromFace(sourceFace);
                    if (sourceTransform != null)
                    {
                        var targets = new List<Transform> { picked.transform };
                        yield return CoLaunchDieToDieProjectiles(sourceTransform, targets, null, launchIcon);
                    }

                    _pendingTopFaceByDieIndex[idx] = null;
                    _pendingDieSourceByIndex[idx] = null;
                    spawner.RerollDiePhysics(picked);
                    yield return new WaitUntil(() => _pendingTopFaceByDieIndex[idx] != null);
                    var newFace = _pendingTopFaceByDieIndex[idx];
                    if (newFace != null)
                    {
                        _pendingRerollGrants += CountRerollGrantsOnFace(newFace, RerollDieAction.RerollDieScope.PlayerChoosesAnyDie);
                        yield return CoProcessTriggeringRerollsAtDieIndex(idx);
                    }
                }
            }
        }

        PrepareBatchRerollOtherDiceTargets();
        PrepareBatchIncreaseOtherElementsTargets();

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
            if (dieAsset != null)
                _batchDieAssetByGatherIndex[i] = dieAsset;
            var skipPower = _noPowerOnNextGatherCommit.Remove(i);
            CommitResolvedRoll(f, t, dieAsset, i, skipPower);
            yield return CoDrainGemScheduledRerolls();
        }

        if (_batchHasRerollOtherDicePending)
        {
            _pendingBatchSubmitForRerollOther.Clear();
            _postBatchOtherDiceRerollStarted = false;
            SnapshotBatchLiveDiceForRerollOther(batchGatherStart);
            for (var i = batchGatherStart; i < channeledFaces.Count; i++)
                _pendingBatchSubmitForRerollOther.Add(channeledFaces[i]);
        }

        QueueAddPowerChoicesAfterBatchGather(batchGatherStart, channeledFaces.Count);
        ApplyPostBatchFaceEffects(batchGatherStart, channeledFaces.Count);

        if (currentPower > maxPower)
            AbortDeferredPostSubmitRerolls();

        _rollBatchPipelineRunning = false;
        _pendingTopFaceByDieIndex = null;
        _pendingDieSourceByIndex = null;
        _pendingBatchDiceAssets = null;
        _batchDieAssetByGatherIndex.Clear();

        yield return new WaitUntil(() => pendingRollVisualRaiseSequences <= 0);
        if (_batchHasIncreaseOtherElementsPending)
            yield return CoExecuteBatchIncreaseOtherElements();
        if (ShouldDeferBatchOutcomeForPostSubmitReroll())
            _postRaiseCombatGateOpen = true;
        else
            ProcessPrecisionQueue();
    }

    /// <summary>
    /// Post-submit Roll Again and reroll-other defer perfect cast until reroll outcomes finish.
    /// Bust resolves immediately after the initial gather (pending rerolls are aborted).
    /// </summary>
    private bool ShouldDeferBatchOutcomeForPostSubmitReroll()
    {
        if (currentPower > maxPower)
            return false;

        if (_deferredTriggeringRerollFacesRemaining > 0)
            return true;

        if (_batchHasRerollOtherDicePending && !_postBatchOtherDiceRerollCompleted)
            return true;

        return false;
    }

    private bool HasPendingDeferredRerolls()
    {
        if (currentPower > maxPower)
            return false;

        return _deferredTriggeringRerollFacesRemaining > 0
            || (_batchHasRerollOtherDicePending && !_postBatchOtherDiceRerollCompleted);
    }

    private void AbortDeferredPostSubmitRerolls()
    {
        _deferredTriggeringRerollFacesRemaining = 0;
        _facesAwaitingPostSubmitTriggeringReroll.Clear();
        ClearBatchRerollOtherDiceState();
        diceRollOutcomeFlyout?.ClearParkedRerollFlyouts();
        ClearBatchIncreaseOtherElementsState();
    }

    private IEnumerator CoAfterRollVisualsThen(Action onComplete)
    {
        yield return new WaitUntil(() => pendingRollVisualSequences <= 0);
        onComplete?.Invoke();
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

    private void PrepareBatchRerollOtherDiceTargets()
    {
        _batchHasRerollOtherDicePending = false;
        _postBatchOtherDiceRerollCompleted = true;
        _batchRerollOtherTargetIndices.Clear();
        _batchRerollOtherKeeperIndices.Clear();

        if (_pendingTopFaceByDieIndex == null || expectedDiceCount <= 1)
            return;

        var keeperIndices = new HashSet<int>();
        for (var i = 0; i < expectedDiceCount; i++)
        {
            if (FaceHasRerollOtherDiceAfterAllSettled(_pendingTopFaceByDieIndex[i]))
                keeperIndices.Add(i);
        }

        if (keeperIndices.Count == 0)
            return;

        foreach (var keeperIdx in keeperIndices)
            _batchRerollOtherKeeperIndices.Add(keeperIdx);

        for (var i = 0; i < expectedDiceCount; i++)
        {
            if (!keeperIndices.Contains(i))
                _batchRerollOtherTargetIndices.Add(i);
        }

        if (_batchRerollOtherTargetIndices.Count == 0)
            return;

        _batchHasRerollOtherDicePending = true;
        _postBatchOtherDiceRerollCompleted = false;
    }

    private void PrepareBatchIncreaseOtherElementsTargets()
    {
        _batchHasIncreaseOtherElementsPending = false;
        _batchIncreaseOtherGrants.Clear();

        if (_pendingTopFaceByDieIndex == null || expectedDiceCount <= 1)
            return;

        for (var i = 0; i < expectedDiceCount; i++)
        {
            var face = _pendingTopFaceByDieIndex[i];
            if (face?.actions == null)
                continue;

            foreach (var action in face.actions)
            {
                if (action is not IncreaseOtherElementsAction increase)
                    continue;

                _batchIncreaseOtherGrants.Add(new IncreaseOtherBatchGrant
                {
                    KeeperBatchIndex = i,
                    Action = increase,
                });
            }
        }

        if (_batchIncreaseOtherGrants.Count > 0)
            _batchHasIncreaseOtherElementsPending = true;
    }

    private void ClearBatchIncreaseOtherElementsState()
    {
        _batchHasIncreaseOtherElementsPending = false;
        _batchIncreaseOtherGrants.Clear();
    }

    private FaceResult FindIncreaseTargetFace(int batchGatherIndex)
    {
        FaceResult match = null;
        for (var i = 0; i < channeledFaces.Count; i++)
        {
            var face = channeledFaces[i];
            if (face == null || face.BatchGatherIndex != batchGatherIndex || face.BatchId != _rollBatchId)
                continue;
            if (face.AwaitingPostBatchOtherDiceReroll)
                continue;

            match = face;
        }

        return match;
    }

    private List<int> ResolveIncreaseOtherTargetIndices(int keeperBatchIndex, IncreaseOtherElementsAction action)
    {
        var matching = new List<int>();
        for (var i = 0; i < expectedDiceCount; i++)
        {
            if (i == keeperBatchIndex)
                continue;

            var face = FindIncreaseTargetFace(i);
            if (face == null)
                continue;

            if (!IncreaseOtherElementsAction.TryGetMatchingPoolRow(face, action, out _))
                continue;

            matching.Add(i);
        }

        if (action.OnlyOneRandomTarget && matching.Count > 1)
        {
            var pick = matching[UnityEngine.Random.Range(0, matching.Count)];
            matching.Clear();
            matching.Add(pick);
        }

        return matching;
    }

    private IEnumerator CoExecuteBatchIncreaseOtherElements()
    {
        try
        {
            if (_batchIncreaseOtherGrants.Count == 0)
                yield break;

            foreach (var grant in _batchIncreaseOtherGrants)
            {
                var action = grant.Action;
                if (action == null)
                    continue;

                var targetIndices = ResolveIncreaseOtherTargetIndices(grant.KeeperBatchIndex, action);
                if (targetIndices.Count == 0)
                    continue;

                var targetTransforms = new List<Transform>();
                var hitTargets = new List<IncreaseOtherHitTarget>();
                foreach (var targetIdx in targetIndices)
                {
                    var face = FindIncreaseTargetFace(targetIdx);
                    if (face == null)
                        continue;

                    if (!IncreaseOtherElementsAction.TryGetMatchingPoolRow(face, action, out var rowKey))
                        continue;

                    var targetTransform = GetBatchDieTransform(targetIdx);
                    if (targetTransform == null)
                    {
                        Debug.LogWarning($"CombatManager: IncreaseOtherElements — no target transform for batch index {targetIdx}.");
                        continue;
                    }

                    targetTransforms.Add(targetTransform);
                    hitTargets.Add(new IncreaseOtherHitTarget { Face = face, RowKey = rowKey });
                }

                if (targetTransforms.Count == 0)
                    continue;

                var source = GetBatchDieTransform(grant.KeeperBatchIndex);
                if (source == null)
                {
                    Debug.LogWarning($"CombatManager: IncreaseOtherElements — no source transform for keeper batch index {grant.KeeperBatchIndex}.");
                    continue;
                }

                var bonus = action.BonusAmount;
                yield return CoLaunchIncreaseOtherProjectiles(
                    source,
                    targetTransforms,
                    hitTargets,
                    bonus,
                    grant.KeeperBatchIndex);
            }

            NotifyStoredActionsPoolUpdated();
        }
        finally
        {
            ClearBatchIncreaseOtherElementsState();
        }
    }

    private IEnumerator CoLaunchIncreaseOtherProjectiles(
        Transform source,
        IReadOnlyList<Transform> targets,
        IReadOnlyList<IncreaseOtherHitTarget> hitTargets,
        int bonusAmount,
        int sourceBatchIndex)
    {
        if (source == null || targets == null || targets.Count == 0 || hitTargets == null || hitTargets.Count == 0)
            yield break;

        diceRollOutcomeFlyout?.RemoveIncreaseOtherSourceFlyout(sourceBatchIndex);

        var prefab = ResolveDieToDieProjectilePrefab(null);
        if (dieToDieProjectileController == null || prefab == null)
            yield break;

        if (sourceBatchIndex >= 0)
            _deferDissolveBatchIndicesForDieToDieLaunch.Add(sourceBatchIndex);

        yield return dieToDieProjectileController.LaunchAndWait(source, targets, prefab, targetIndex =>
        {
            if (targetIndex < 0 || targetIndex >= hitTargets.Count)
                return;

            var hit = hitTargets[targetIndex];
            if (hit.Face == null)
                return;

            IncreaseOtherElementsAction.ApplyBonusToFace(hit.Face, hit.RowKey, bonusAmount);
            diceRollOutcomeFlyout?.TryApplyFlyoutLineBonus(hit.Face.BatchGatherIndex, hit.RowKey, bonusAmount);
        });

        if (sourceBatchIndex >= 0)
            TryDissolveDeferredDieToDieSource(sourceBatchIndex);
    }

    private static bool FaceHasRerollOtherDiceAfterAllSettled(DieFaceSO face)
    {
        if (face?.actions == null) return false;
        for (var i = 0; i < face.actions.Count; i++)
        {
            if (face.actions[i] is RerollOtherDiceAfterAllSettledAction)
                return true;
        }

        return false;
    }

    private void BuildPendingPlayerChoiceRerollSourceQueue()
    {
        _pendingPlayerChoiceRerollSources.Clear();
        if (_pendingTopFaceByDieIndex == null)
            return;

        for (var i = 0; i < expectedDiceCount; i++)
        {
            var grants = CountRerollGrantsOnFace(_pendingTopFaceByDieIndex[i], RerollDieAction.RerollDieScope.PlayerChoosesAnyDie);
            for (var g = 0; g < grants; g++)
                _pendingPlayerChoiceRerollSources.Enqueue(i);
        }
    }

    private Transform GetBatchDieTransform(int batchIndex)
    {
        if (_pendingDieSourceByIndex != null
            && batchIndex >= 0
            && batchIndex < _pendingDieSourceByIndex.Length
            && _pendingDieSourceByIndex[batchIndex] != null)
            return _pendingDieSourceByIndex[batchIndex];

        if (_batchLiveDieByGatherIndex.TryGetValue(batchIndex, out var live) && live != null)
            return live.transform;

        var go = spawner != null ? spawner.GetActiveDieGameObject(batchIndex) : null;
        return go != null ? go.transform : null;
    }

    private GameObject ResolveDieToDieProjectilePrefab(GameObject actionPrefab) =>
        actionPrefab != null ? actionPrefab : defaultDieToDieProjectilePrefab;

    private IEnumerator CoLaunchDieToDieProjectiles(
        Transform source,
        IReadOnlyList<Transform> targets,
        GameObject actionPrefab,
        DieToDieLaunchIcon launchIcon = default)
    {
        if (source == null || targets == null || targets.Count == 0)
            yield break;

        var prefab = ResolveDieToDieProjectilePrefab(actionPrefab);
        var hasProjectiles = dieToDieProjectileController != null && prefab != null;
        var hasFlyouts = diceRollOutcomeFlyout != null && launchIcon.HasAny;

        if (!hasProjectiles && !hasFlyouts)
            yield break;

        var sourceBatchIndex = spawner != null ? spawner.GetIndexOfActiveDie(source.gameObject) : -1;
        if (!hasFlyouts)
            diceRollOutcomeFlyout?.ConsumeParkedDieToDieActionFlyout(sourceBatchIndex);
        if (sourceBatchIndex >= 0 && hasProjectiles)
            _deferDissolveBatchIndicesForDieToDieLaunch.Add(sourceBatchIndex);

        float? flyDuration = null;
        if (prefab != null && prefab.TryGetComponent<DieToDieProjectileFlight>(out var templateFlight))
            flyDuration = templateFlight.FlightSettings.flyDuration;

        var routinesRemaining = 0;
        if (hasProjectiles)
            routinesRemaining++;
        if (hasFlyouts)
            routinesRemaining++;

        if (hasProjectiles)
        {
            StartCoroutine(CoRunDieToDieRoutineThenSignal(
                dieToDieProjectileController.LaunchAndWait(source, targets, prefab),
                () => routinesRemaining--));
        }

        if (hasFlyouts)
        {
            StartCoroutine(CoRunDieToDieRoutineThenSignal(
                diceRollOutcomeFlyout.CoLaunchParkedRerollFlyouts(sourceBatchIndex, source, targets, launchIcon, flyDuration),
                () => routinesRemaining--));
        }

        yield return new WaitUntil(() => routinesRemaining <= 0);

        if (sourceBatchIndex >= 0)
            TryDissolveDeferredDieToDieSource(sourceBatchIndex);
    }

    static IEnumerator CoRunDieToDieRoutineThenSignal(IEnumerator routine, Action onComplete)
    {
        if (routine != null)
            yield return routine;
        onComplete?.Invoke();
    }

    static DieToDieLaunchIcon ResolveDieToDieLaunchIconFromFace(DieFaceSO face)
    {
        if (face?.actions == null)
            return default;

        for (var i = 0; i < face.actions.Count; i++)
        {
            if (face.actions[i] is RerollOtherDiceAfterAllSettledAction)
                return DieToDieLaunchIcon.FromActionVisualId(ActionVisualId.RerollOtherDice);
            if (face.actions[i] is RerollDieAction)
                return DieToDieLaunchIcon.FromActionVisualId(ActionVisualId.RerollDie);
        }

        return default;
    }

    private DieFaceSO FindKeeperFaceForBatchIndex(int keeperIdx)
    {
        for (var i = channeledFaces.Count - 1; i >= 0; i--)
        {
            var faceResult = channeledFaces[i];
            if (faceResult != null
                && faceResult.BatchGatherIndex == keeperIdx
                && !faceResult.AwaitingPostBatchOtherDiceReroll)
                return faceResult.Face;
        }

        if (_pendingTopFaceByDieIndex != null
            && keeperIdx >= 0
            && keeperIdx < _pendingTopFaceByDieIndex.Length)
            return _pendingTopFaceByDieIndex[keeperIdx];

        return null;
    }

    private void TryDissolveDeferredDieToDieSource(int batchIndex)
    {
        if (!_deferDissolveBatchIndicesForDieToDieLaunch.Remove(batchIndex))
            return;

        var dieTransform = GetBatchDieTransform(batchIndex);
        if (dieTransform != null && spawner != null)
            spawner.BeginDissolveAndDestroyDie(dieTransform.gameObject);
    }

    private IEnumerator CoPlayDieToDieRerollOtherProjectiles()
    {
        if (!_batchHasRerollOtherDicePending
            || _batchRerollOtherKeeperIndices.Count == 0
            || _batchRerollOtherTargetIndices.Count == 0)
            yield break;

        var targetTransforms = new List<Transform>();
        foreach (var targetIdx in _batchRerollOtherTargetIndices)
        {
            var target = GetBatchDieTransform(targetIdx);
            if (target == null)
            {
                Debug.LogWarning($"CombatManager: Post-batch reroll other — no target transform for batch index {targetIdx}.");
                continue;
            }

            targetTransforms.Add(target);
        }

        if (targetTransforms.Count == 0)
            yield break;

        foreach (var keeperIdx in _batchRerollOtherKeeperIndices)
        {
            var source = GetBatchDieTransform(keeperIdx);
            if (source == null)
            {
                Debug.LogWarning($"CombatManager: Post-batch reroll other — no source transform for keeper batch index {keeperIdx}.");
                continue;
            }

            var keeperFace = FindKeeperFaceForBatchIndex(keeperIdx);
            var launchIcon = ResolveDieToDieLaunchIconFromFace(keeperFace);
            yield return CoLaunchDieToDieProjectiles(source, targetTransforms, null, launchIcon);
        }
    }

    IEnumerator CoProcessTriggeringRerollsAtDieIndex(int dieIdx)
    {
        if (_pendingTopFaceByDieIndex == null || dieIdx < 0 || dieIdx >= _pendingTopFaceByDieIndex.Length)
            yield break;

        var pendingFace = _pendingTopFaceByDieIndex[dieIdx];
        var grantsRemaining = CountRerollGrantsOnFace(pendingFace, RerollDieAction.RerollDieScope.RerollTriggeringDieOnly);
        while (grantsRemaining > 0 && pendingFace != null)
        {
            grantsRemaining--;
            if (!TryGetTriggeringRerollAction(pendingFace, out var rerollAction))
                break;

            var dieGo = spawner.GetActiveDieGameObject(dieIdx);
            if (dieGo == null)
            {
                Debug.LogWarning($"CombatManager: Auto reroll — no die at batch index {dieIdx}.");
                yield break;
            }

            var keepFace = rerollAction.KeepSameFaceOnReroll;
            var faceToCommit = keepFace ? pendingFace : null;

            _pendingTopFaceByDieIndex[dieIdx] = null;
            if (!keepFace)
                _pendingDieSourceByIndex[dieIdx] = null;

            spawner.RerollDiePhysics(dieGo);
            yield return new WaitUntil(() => _pendingTopFaceByDieIndex[dieIdx] != null);

            if (keepFace && faceToCommit != null)
                _pendingTopFaceByDieIndex[dieIdx] = faceToCommit;

            pendingFace = _pendingTopFaceByDieIndex[dieIdx];
            if (!keepFace && pendingFace != null)
                grantsRemaining += CountRerollGrantsOnFace(pendingFace, RerollDieAction.RerollDieScope.RerollTriggeringDieOnly);
        }
    }

    static bool TryGetTriggeringRerollAction(DieFaceSO face, out RerollDieAction reroll)
    {
        reroll = null;
        if (face?.actions == null)
            return false;

        for (var i = 0; i < face.actions.Count; i++)
        {
            if (face.actions[i] is RerollDieAction r && r.Scope == RerollDieAction.RerollDieScope.RerollTriggeringDieOnly)
            {
                reroll = r;
                return true;
            }
        }

        return false;
    }

    private int CountRerollGrantsFromAllPendingFaces(RerollDieAction.RerollDieScope scope)
    {
        if (_pendingTopFaceByDieIndex == null) return 0;
        var n = 0;
        for (var i = 0; i < _pendingTopFaceByDieIndex.Length; i++)
        {
            var f = _pendingTopFaceByDieIndex[i];
            if (f == null) continue;
            n += CountRerollGrantsOnFace(f, scope);
        }

        return n;
    }

    void ApplyPostBatchFaceEffects(int startInclusive, int endExclusive)
    {
        var perfectCast = currentPower == maxPower;
        for (var i = startInclusive; i < endExclusive; i++)
        {
            if (i < 0 || i >= channeledFaces.Count) continue;
            var fr = channeledFaces[i];
            if (fr?.Actions == null) continue;

            foreach (var a in fr.Actions)
            {
                if (a is not ReducePowerUnlessPerfectCastAfterBatchAction reduce || perfectCast)
                    continue;

                var amount = reduce.PowerReduction;
                if (amount <= 0)
                    continue;

                currentPower = Mathf.Max(0, currentPower - amount);
                if (GameActionDebug.Enabled)
                    Debug.Log($"[ReducePowerUnlessPerfectCast] Power reduced by {amount} (no perfect cast). New power: {currentPower}/{maxPower}");
            }
        }

        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
    }

    private IEnumerator CoDrainGemScheduledRerolls()
    {
        while (_gemScheduledBatchRerolls.Count > 0)
        {
            var batch = new List<GemBatchRerollSchedule>(_gemScheduledBatchRerolls);
            _gemScheduledBatchRerolls.Clear();

            var byTrigger = new Dictionary<int, List<GemBatchRerollSchedule>>();
            foreach (var entry in batch)
            {
                if (!byTrigger.TryGetValue(entry.TriggerGatherIndex, out var group))
                {
                    group = new List<GemBatchRerollSchedule>();
                    byTrigger.Add(entry.TriggerGatherIndex, group);
                }

                group.Add(entry);
            }

            foreach (var group in byTrigger.Values)
            {
                if (group.Count == 0)
                    continue;

                var triggerIdx = group[0].TriggerGatherIndex;
                var source = GetBatchDieTransform(triggerIdx);
                var targetTransforms = new List<Transform>();
                var targetIndices = new List<int>();
                GameObject projectilePrefab = null;

                foreach (var entry in group)
                {
                    var j = entry.TargetIndex;
                    if (j < 0 || j >= expectedDiceCount)
                        continue;
                    if (_pendingTopFaceByDieIndex == null || j >= _pendingTopFaceByDieIndex.Length)
                        continue;

                    var targetTransform = GetBatchDieTransform(j);
                    if (targetTransform == null)
                    {
                        Debug.LogError($"CombatManager: Gem batch reroll — no active die transform for batch index {j}.");
                        _gemBatchRerollIndicesInFlight.Remove(j);
                        continue;
                    }

                    targetTransforms.Add(targetTransform);
                    targetIndices.Add(j);
                    if (projectilePrefab == null && entry.ProjectilePrefab != null)
                        projectilePrefab = entry.ProjectilePrefab;
                }

                if (source != null && targetTransforms.Count > 0)
                    yield return CoLaunchDieToDieProjectiles(source, targetTransforms, projectilePrefab);

                foreach (var j in targetIndices)
                {
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
                    if (_pendingTopFaceByDieIndex == null)
                        return true;

                    foreach (var j in targetIndices)
                    {
                        if (j < 0 || j >= _pendingTopFaceByDieIndex.Length)
                            continue;
                        if (_pendingTopFaceByDieIndex[j] == null)
                            return false;
                    }

                    return true;
                });
            }
        }
    }

    public bool FaceHasPendingPostSubmitTriggeringReroll(FaceResult face) =>
        face != null && _facesAwaitingPostSubmitTriggeringReroll.Contains(face);

    public bool FaceHasPendingPostBatchOtherDiceReroll(FaceResult face) =>
        face != null && _facesAwaitingPostBatchOtherDiceReroll.Contains(face) && _batchHasRerollOtherDicePending;

    public bool FaceBlocksDieDissolveForPendingReroll(FaceResult face) =>
        FaceHasPendingPostSubmitTriggeringReroll(face)
        || FaceHasPendingPostBatchOtherDiceReroll(face)
        || ShouldHoldDieAliveForBatchRerollOther(face);

    /// <summary>Source die for a die-to-die projectile stays visible until projectiles arrive and reroll begins.</summary>
    public bool DieTransformBlocksDissolveForDieToDieDeferred(Transform dieTransform)
    {
        if (dieTransform == null || spawner == null)
            return false;

        var batchIndex = spawner.GetIndexOfActiveDie(dieTransform.gameObject);
        return batchIndex >= 0 && _deferDissolveBatchIndicesForDieToDieLaunch.Contains(batchIndex);
    }

    private void TryTriggerPostBatchRerollOtherIfReady()
    {
        if (!_batchHasRerollOtherDicePending || _postBatchOtherDiceRerollStarted)
            return;
        if (_pendingBatchSubmitForRerollOther.Count > 0)
            return;

        _postBatchOtherDiceRerollStarted = true;
        StartCoroutine(CoExecutePostBatchRerollOtherDice());
    }

    private void ClearBatchRerollOtherDiceState()
    {
        _batchHasRerollOtherDicePending = false;
        _postBatchOtherDiceRerollCompleted = true;
        _postBatchOtherDiceRerollStarted = false;
        _batchRerollOtherTargetIndices.Clear();
        _batchRerollOtherKeeperIndices.Clear();
        _facesAwaitingPostBatchOtherDiceReroll.Clear();
        _pendingBatchSubmitForRerollOther.Clear();
        _postBatchSecondPassAwaitingSubmit.Clear();
        _postBatchRollAgainInFlight = 0;
        _batchLiveDieByGatherIndex.Clear();
    }

    private void SnapshotBatchLiveDiceForRerollOther(int batchGatherStart)
    {
        _batchLiveDieByGatherIndex.Clear();
        for (var i = batchGatherStart; i < channeledFaces.Count; i++)
        {
            var face = channeledFaces[i];
            if (face == null || face.BatchGatherIndex < 0)
                continue;

            if (face.DieSource != null)
                _batchLiveDieByGatherIndex[face.BatchGatherIndex] = face.DieSource.gameObject;
            else if (spawner != null)
            {
                var die = spawner.GetActiveDieGameObject(face.BatchGatherIndex);
                if (die != null)
                    _batchLiveDieByGatherIndex[face.BatchGatherIndex] = die;
            }
        }
    }

    private bool ShouldHoldDieAliveForBatchRerollOther(FaceResult face)
    {
        if (face == null || !_batchHasRerollOtherDicePending || _postBatchOtherDiceRerollCompleted)
            return false;

        return face.BatchId == _rollBatchId;
    }

    private GameObject ResolveBatchDieGameObject(int batchGatherIndex, FaceResult firstPassFace = null)
    {
        if (firstPassFace?.DieSource != null)
            return firstPassFace.DieSource.gameObject;

        if (_batchLiveDieByGatherIndex.TryGetValue(batchGatherIndex, out var snap) && snap != null)
            return snap;

        return spawner != null ? spawner.GetActiveDieGameObject(batchGatherIndex) : null;
    }

    private void DropFailedBatchRerollOtherTarget(int dieIdx)
    {
        _batchRerollOtherTargetIndices.Remove(dieIdx);
        _batchLiveDieByGatherIndex.Remove(dieIdx);

        for (var i = channeledFaces.Count - 1; i >= 0; i--)
        {
            var face = channeledFaces[i];
            if (face == null || face.BatchGatherIndex != dieIdx || !face.AwaitingPostBatchOtherDiceReroll)
                continue;

            _facesAwaitingPostBatchOtherDiceReroll.Remove(face);
            break;
        }
    }

    /// <summary>
    /// Called when every flyout line and drag token for <paramref name="face"/> has been applied.
    /// Triggers a deferred <see cref="RerollDieAction"/> reroll or dissolves the die.
    /// </summary>
    public void NotifyFaceOutcomesSubmitted(FaceResult face)
    {
        if (face == null || !_faceOutcomesSubmitted.Add(face))
            return;

        _pendingBatchSubmitForRerollOther.Remove(face);
        TryTriggerPostBatchRerollOtherIfReady();

        if (_facesAwaitingPostSubmitTriggeringReroll.Remove(face))
        {
            _postBatchSecondPassAwaitingSubmit.Remove(face);
            StartCoroutine(CoExecutePostSubmitTriggeringReroll(face));
            TryCompletePostBatchRerollOtherDiceSequence();
            return;
        }

        if (_postBatchSecondPassAwaitingSubmit.Remove(face))
        {
            OnPostBatchSecondPassFaceSubmitted(face);
            return;
        }

        if (FaceHasPendingPostBatchOtherDiceReroll(face) || ShouldHoldDieAliveForBatchRerollOther(face))
            return;

        TryDissolveDieForFace(face);
    }

    private void OnPostBatchSecondPassFaceSubmitted(FaceResult face)
    {
        if (face?.Face != null &&
            CountRerollGrantsOnFace(face.Face, RerollDieAction.RerollDieScope.RerollTriggeringDieOnly) > 0)
        {
            _postBatchRollAgainInFlight++;
            StartCoroutine(CoPostSecondPassRollAgainChainWrapped(face));
        }
        else
        {
            TryDissolveDieForFace(face);
        }

        TryCompletePostBatchRerollOtherDiceSequence();
    }

    private IEnumerator CoPostSecondPassRollAgainChainWrapped(FaceResult sourceFaceResult)
    {
        try
        {
            yield return CoPostSecondPassRollAgainChainForOtherDie(sourceFaceResult);
        }
        finally
        {
            if (_postBatchRollAgainInFlight > 0)
                _postBatchRollAgainInFlight--;
            TryCompletePostBatchRerollOtherDiceSequence();
        }
    }

    private void TryCompletePostBatchRerollOtherDiceSequence()
    {
        if (_postBatchOtherDiceRerollCompleted)
            return;
        if (_postBatchSecondPassAwaitingSubmit.Count > 0 || _postBatchRollAgainInFlight > 0)
            return;

        CompletePostBatchRerollOtherDiceSequence();
    }

    private void CompletePostBatchRerollOtherDiceSequence()
    {
        if (_postBatchOtherDiceRerollCompleted)
            return;

        foreach (var firstPassFace in _facesAwaitingPostBatchOtherDiceReroll)
            TryDissolveDieForFace(firstPassFace);
        _facesAwaitingPostBatchOtherDiceReroll.Clear();
        _postBatchOtherDiceRerollCompleted = true;
        _batchHasRerollOtherDicePending = false;
        _batchRerollOtherTargetIndices.Clear();
        _pendingBatchSubmitForRerollOther.Clear();
        _postBatchSecondPassAwaitingSubmit.Clear();
        _batchLiveDieByGatherIndex.Clear();
        TryFinalizeBatchOutcomeAfterDeferredRerolls();
    }

    private void BeginAwaitingPostBatchSecondPassSubmit(IReadOnlyList<FaceResult> secondPassResults)
    {
        _postBatchSecondPassAwaitingSubmit.Clear();
        if (secondPassResults != null)
        {
            foreach (var result in secondPassResults)
            {
                if (result != null)
                    _postBatchSecondPassAwaitingSubmit.Add(result);
            }
        }

        if (_postBatchSecondPassAwaitingSubmit.Count == 0)
            TryCompletePostBatchRerollOtherDiceSequence();
    }

    private void TryDissolveDieForFace(FaceResult face)
    {
        if (face?.DieSource == null || spawner == null)
            return;

        spawner.BeginDissolveAndDestroyDie(face.DieSource.gameObject);
    }

    private void ClearPostSubmitTriggeringRerollState()
    {
        _facesAwaitingPostSubmitTriggeringReroll.Clear();
        _faceOutcomesSubmitted.Clear();
        _postSubmitRerollWaitingIndices.Clear();
        _postSubmitRerollSettledFaces.Clear();
        _batchDieAssetByGatherIndex.Clear();
        _deferredTriggeringRerollFacesRemaining = 0;
        ClearBatchRerollOtherDiceState();
    }

    private void CompleteDeferredTriggeringRerollFace()
    {
        if (_deferredTriggeringRerollFacesRemaining <= 0)
            return;

        _deferredTriggeringRerollFacesRemaining--;
        TryFinalizeBatchOutcomeAfterDeferredRerolls();
    }

    private void TryFinalizeBatchOutcomeAfterDeferredRerolls()
    {
        if (_deferredTriggeringRerollFacesRemaining > 0)
            return;
        if (_batchHasRerollOtherDicePending && !_postBatchOtherDiceRerollCompleted)
            return;

        ProcessPrecisionQueue();
    }

    private IEnumerator CoWaitUntilFaceOutcomesSubmitted(FaceResult face)
    {
        if (face == null || _faceOutcomesSubmitted.Contains(face))
            yield break;

        const float timeoutSeconds = 120f;
        var elapsed = 0f;
        while (!_faceOutcomesSubmitted.Contains(face) && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!_faceOutcomesSubmitted.Contains(face))
            Debug.LogError($"CombatManager: Timed out waiting for face '{face.Face?.name}' outcomes to submit before post-submit reroll dissolve.");
    }

    private IEnumerator CoExecutePostSubmitTriggeringReroll(FaceResult sourceFaceResult)
    {
        try
        {
            if (sourceFaceResult == null || spawner == null)
                yield break;

            var face = sourceFaceResult.Face;
            var grantsRemaining = CountRerollGrantsOnFace(face, RerollDieAction.RerollDieScope.RerollTriggeringDieOnly);
            var dieTransform = sourceFaceResult.DieSource;
            var dieIdx = sourceFaceResult.BatchGatherIndex;

            while (grantsRemaining > 0 && face != null)
            {
                grantsRemaining--;
                if (!TryGetTriggeringRerollAction(face, out var rerollAction))
                    break;

                if (rerollAction.SkipRerollWhenPerfectCast && QualifiesForPerfectCast())
                    break;

                var dieGo = dieTransform != null ? dieTransform.gameObject : spawner.GetActiveDieGameObject(dieIdx);
                if (dieGo == null)
                {
                    Debug.LogWarning($"CombatManager: Post-submit reroll — no die for batch index {dieIdx}.");
                    yield break;
                }

                dieTransform = dieGo.transform;
                var keepFace = rerollAction.KeepSameFaceOnReroll;

                _postSubmitRerollSettledFaces.Remove(dieIdx);
                _postSubmitRerollWaitingIndices.Add(dieIdx);
                spawner.RerollDiePhysics(dieGo);
                yield return new WaitUntil(() => !_postSubmitRerollWaitingIndices.Contains(dieIdx));

                DieFaceSO newFace;
                if (keepFace)
                    newFace = face;
                else if (!_postSubmitRerollSettledFaces.TryGetValue(dieIdx, out newFace) || newFace == null)
                {
                    Debug.LogError($"CombatManager: Post-submit reroll — no settled face for batch index {dieIdx}.");
                    yield break;
                }

                _postSubmitRerollSettledFaces.Remove(dieIdx);

                var dieAsset = sourceFaceResult.SourceDieAsset;
                if (dieAsset == null && dieIdx >= 0)
                    _batchDieAssetByGatherIndex.TryGetValue(dieIdx, out dieAsset);

                CommitResolvedRoll(newFace, dieTransform, dieAsset, dieIdx, skipPowerContribution: true, allowPostSubmitTriggeringReroll: false);

                face = newFace;
                if (!keepFace && face != null)
                    grantsRemaining += CountRerollGrantsOnFace(face, RerollDieAction.RerollDieScope.RerollTriggeringDieOnly);

                if (channeledFaces.Count > 0)
                {
                    var latestResult = channeledFaces[channeledFaces.Count - 1];
                    yield return CoWaitUntilFaceOutcomesSubmitted(latestResult);
                }
            }
        }
        finally
        {
            CompleteDeferredTriggeringRerollFace();
            TryDissolveDieForFace(sourceFaceResult);
            TryCompletePostBatchRerollOtherDiceSequence();
        }
    }

    private FaceResult FindFirstPassFaceAwaitingPostBatchOtherDiceReroll(int dieIdx)
    {
        for (var i = 0; i < channeledFaces.Count; i++)
        {
            var fr = channeledFaces[i];
            if (fr != null && fr.BatchGatherIndex == dieIdx && fr.AwaitingPostBatchOtherDiceReroll)
                return fr;
        }

        return null;
    }

    private IEnumerator CoExecutePostBatchRerollOtherDice()
    {
        try
        {
            if (spawner == null)
                yield break;

            var indices = new List<int>(_batchRerollOtherTargetIndices);
            indices.Sort();

            yield return CoPlayDieToDieRerollOtherProjectiles();

            var settledFaces = new Dictionary<int, DieFaceSO>();
            yield return CoParallelPhysicsRerollOtherDice(indices, settledFaces);

            _postRaiseCombatGateOpen = true;

            var secondPassResults = new List<FaceResult>();
            foreach (var dieIdx in indices)
            {
                if (!settledFaces.TryGetValue(dieIdx, out var face) || face == null)
                    continue;

                var result = TryCommitSecondPassOtherDie(dieIdx, face);
                if (result != null)
                    secondPassResults.Add(result);
            }

            BeginAwaitingPostBatchSecondPassSubmit(secondPassResults);
        }
        finally
        {
            TryCompletePostBatchRerollOtherDiceSequence();
        }
    }

    private IEnumerator CoParallelPhysicsRerollOtherDice(IReadOnlyList<int> indices, Dictionary<int, DieFaceSO> settledFacesOut)
    {
        settledFacesOut.Clear();
        if (indices == null || indices.Count == 0 || spawner == null)
            yield break;

        var launched = new List<int>();
        foreach (var dieIdx in indices)
        {
            var firstPassFace = FindFirstPassFaceAwaitingPostBatchOtherDiceReroll(dieIdx);
            var dieGo = ResolveBatchDieGameObject(dieIdx, firstPassFace);
            if (dieGo == null)
            {
                Debug.LogWarning($"CombatManager: Post-batch reroll other — no die for batch index {dieIdx}.");
                DropFailedBatchRerollOtherTarget(dieIdx);
                continue;
            }

            _batchLiveDieByGatherIndex[dieIdx] = dieGo;
            _postSubmitRerollSettledFaces.Remove(dieIdx);
            _postSubmitRerollWaitingIndices.Add(dieIdx);
            launched.Add(dieIdx);
            spawner.RerollDiePhysics(dieGo);
        }

        if (launched.Count == 0)
            yield break;

        yield return new WaitUntil(() =>
        {
            foreach (var dieIdx in launched)
            {
                if (_postSubmitRerollWaitingIndices.Contains(dieIdx))
                    return false;
            }

            return true;
        });

        foreach (var dieIdx in launched)
        {
            if (_postSubmitRerollSettledFaces.TryGetValue(dieIdx, out var face) && face != null)
                settledFacesOut[dieIdx] = face;
            else
                Debug.LogError($"CombatManager: Post-batch reroll other — no settled face for batch index {dieIdx}.");

            _postSubmitRerollSettledFaces.Remove(dieIdx);
        }
    }

    private FaceResult TryCommitSecondPassOtherDie(int dieIdx, DieFaceSO face)
    {
        var firstPassFace = FindFirstPassFaceAwaitingPostBatchOtherDiceReroll(dieIdx);
        var dieAsset = firstPassFace?.SourceDieAsset;
        var dieGo = ResolveBatchDieGameObject(dieIdx, firstPassFace);
        if (dieGo == null)
        {
            Debug.LogWarning($"CombatManager: Post-batch reroll other — no die for batch index {dieIdx} at commit.");
            DropFailedBatchRerollOtherTarget(dieIdx);
            return null;
        }

        var dieTransform = dieGo.transform;
        _batchLiveDieByGatherIndex[dieIdx] = dieGo;
        var gatherStart = channeledFaces.Count;

        CommitResolvedRoll(
            face,
            dieTransform,
            dieAsset,
            dieIdx,
            skipPowerContribution: true,
            allowPostSubmitTriggeringReroll: true,
            allowPostBatchOtherDiceReroll: false,
            isRerollOtherDiceSecondPass: true);

        return channeledFaces.Count > gatherStart ? channeledFaces[channeledFaces.Count - 1] : null;
    }

    private IEnumerator CoPostSecondPassRollAgainChainForOtherDie(FaceResult sourceFaceResult)
    {
        if (sourceFaceResult == null || spawner == null)
            yield break;

        var face = sourceFaceResult.Face;
        var dieIdx = sourceFaceResult.BatchGatherIndex;
        var dieGo = ResolveBatchDieGameObject(dieIdx, sourceFaceResult);
        if (dieGo == null)
        {
            Debug.LogWarning($"CombatManager: Post-batch Roll Again — no die for batch index {dieIdx}.");
            yield break;
        }

        var dieTransform = dieGo.transform;
        var dieAsset = sourceFaceResult.SourceDieAsset;

        _postSubmitRerollSettledFaces.Remove(dieIdx);
        _postSubmitRerollWaitingIndices.Add(dieIdx);
        spawner.RerollDiePhysics(dieGo);
        yield return new WaitUntil(() => !_postSubmitRerollWaitingIndices.Contains(dieIdx));

        if (!_postSubmitRerollSettledFaces.TryGetValue(dieIdx, out face) || face == null)
        {
            Debug.LogError($"CombatManager: Post-batch Roll Again — no settled face at index {dieIdx}.");
            yield break;
        }

        _postSubmitRerollSettledFaces.Remove(dieIdx);

        CommitResolvedRoll(
            face,
            dieTransform,
            dieAsset,
            dieIdx,
            skipPowerContribution: true,
            allowPostSubmitTriggeringReroll: false,
            allowPostBatchOtherDiceReroll: false,
            isRerollOtherDiceSecondPass: true);

        if (channeledFaces.Count > 0)
        {
            var latestResult = channeledFaces[channeledFaces.Count - 1];
            yield return CoWaitUntilFaceOutcomesSubmitted(latestResult);
        }
    }

    private void CommitResolvedRoll(
        DieFaceSO face,
        Transform dieWorldSource,
        DieAssetSO sourceDieAsset,
        int batchGatherIndex,
        bool skipPowerContribution,
        bool allowPostSubmitTriggeringReroll = true,
        bool allowPostBatchOtherDiceReroll = true,
        bool isRerollOtherDiceSecondPass = false)
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
        result.SourceDieAsset = sourceDieAsset;
        result.BatchGatherIndex = batchGatherIndex;
        result.BatchId = _rollBatchId;

        var awaitingPostSubmitTriggeringReroll = false;
        if (allowPostSubmitTriggeringReroll && TryGetTriggeringRerollAction(face, out _))
        {
            awaitingPostSubmitTriggeringReroll = true;
            result.AwaitingPostSubmitTriggeringReroll = true;
            _facesAwaitingPostSubmitTriggeringReroll.Add(result);
            _deferredTriggeringRerollFacesRemaining++;
        }

        var awaitingPostBatchOtherDiceReroll = allowPostBatchOtherDiceReroll
            && !isRerollOtherDiceSecondPass
            && _batchHasRerollOtherDicePending
            && _batchRerollOtherTargetIndices.Contains(batchGatherIndex);
        if (awaitingPostBatchOtherDiceReroll)
        {
            result.AwaitingPostBatchOtherDiceReroll = true;
            _facesAwaitingPostBatchOtherDiceReroll.Add(result);
        }

        var skipPower = skipPowerContribution
            || isRerollOtherDiceSecondPass
            || (_echoSkipsPowerThisBatch && !isRerollOtherDiceSecondPass);
        result.PowerContributionThisResolve = skipPower ? 0 : modifiedValue;
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
        if (!_echoSkipsPowerThisBatch || isRerollOtherDiceSecondPass)
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
                if (a is RerollOtherDiceAfterAllSettledAction) continue;
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

                ExecuteActionForFaceTargets(result, a, context, ctx => a.Execute(ctx));
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
            var lines = BuildRollVisualLines(
                result,
                kineticArmorThisRoll,
                includeParkedRerollLine: allowPostSubmitTriggeringReroll
                    && allowPostBatchOtherDiceReroll
                    && !isRerollOtherDiceSecondPass);
            if (lines.Count > 0 && CombatEvents.OnDiceRollVisualFeedback != null)
            {
                var activateAfterRegularDice =
                    sourceDieAsset != null &&
                    sourceDieAsset.GetSocketedGems().Any(g =>
                        g?.effects != null &&
                        g.effects.Any(e => e != null && e.kind == GemEffectKind.RandomBatchRerollOtherDiceNoPower));
                pendingRollVisualSequences++;
                pendingRollVisualRaiseSequences++;
                var payload = new DiceRollVisualPayload
                {
                    WorldAnchor = dieWorldSource.position,
                    DieTransform = dieWorldSource,
                    Lines = lines,
                    SourceFace = result,
                    ActivateAfterRegularDice = activateAfterRegularDice,
                    NeedsDelayedStoredPoolResync = lines.Any(l => l.IsVisualFlyoutOnly)
                };
                payload.BindVisualFinished(OnRollVisualSequenceFinished);
                payload.BindRaiseFinished(OnRollVisualRaiseFinished);
                CombatEvents.OnDiceRollVisualFeedback.Invoke(payload);
            }
            else if (result.AwaitingPostSubmitTriggeringReroll || isRerollOtherDiceSecondPass)
                StartCoroutine(CoNotifyFaceSubmittedWhenNoVisuals(result, isRerollOtherDiceSecondPass));
        }
        else if (isRerollOtherDiceSecondPass)
        {
            StartCoroutine(CoNotifyFaceSubmittedWhenNoVisuals(result, isRerollOtherDiceSecondPass: true));
        }
    }

    private IEnumerator CoNotifyFaceSubmittedWhenNoVisuals(FaceResult face, bool isRerollOtherDiceSecondPass = false)
    {
        yield return null;
        if (face == null || _faceOutcomesSubmitted.Contains(face))
            yield break;
        if (!isRerollOtherDiceSecondPass && !_facesAwaitingPostSubmitTriggeringReroll.Contains(face))
            yield break;
        if (targetAssignment != null && targetAssignment.HasPendingTokensForFace(face))
            yield break;

        NotifyFaceOutcomesSubmitted(face);
    }

    private void OnRollVisualRaiseFinished()
    {
        pendingRollVisualRaiseSequences--;
        if (pendingRollVisualRaiseSequences < 0)
        {
            Debug.LogError("CombatManager: pendingRollVisualRaiseSequences underflow — check DiceRollVisualPayload.ReportRaiseFinished is called once per payload.");
            pendingRollVisualRaiseSequences = 0;
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

        if (pendingRollVisualSequences == 0 && currentState != CombatState.BustCheck)
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
            if (a is RerollOtherDiceAfterAllSettledAction) continue;
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

        if (!result.HasEnemyDamagePiece)
            Hint(PoolRowKey.FromDieType(DieType.Damage), result.TotalDamageContribution, GameIconCatalog.GetElementIcon(DieType.Damage));
        Hint(PoolRowKey.FromDieType(DieType.Armor), result.Armor, GameIconCatalog.GetElementIcon(DieType.Armor));
        Hint(PoolRowKey.FromDieType(DieType.Curse), result.TotalSelfDamageContribution, GameIconCatalog.GetElementIcon(DieType.Curse));

        if (result.ActionPoolContributions == null) return;
        foreach (var extra in result.ActionPoolContributions)
        {
            if (IsEnemyTargetedPoolContribution(extra))
                continue;
            Hint(extra.PoolKey, extra.Amount, extra.Icon);
            var bg = extra.PoolRowBackground != null
                ? extra.PoolRowBackground
                : GameIconCatalog.TryGetPoolRowBackground(extra.PoolKey);
            if (extra.Amount > 0 && bg != null)
                CombatEvents.OnRuntimePoolRowBackgroundForRow?.Invoke(extra.PoolKey, bg);
        }
    }

    private static List<RollOutcomeVisualLine> BuildRollVisualLines(
        FaceResult result,
        bool kineticArmorThisRoll,
        bool includeParkedRerollLine)
    {
        var lines = new List<RollOutcomeVisualLine>();

        void AddLine(PoolRowKey key, int amt, Sprite icon, bool enemyTargeted = false)
        {
            if (amt <= 0) return;
            var attackAll = result.AttackAllEnemies && enemyTargeted;
            lines.Add(new RollOutcomeVisualLine
            {
                RowKey = key,
                Amount = amt,
                IconOverride = icon,
                EnemyTargeted = enemyTargeted,
                AttackAllEnemies = attackAll
            });
        }

        var damageIsEnemyTargeted = result.Type == DieType.Damage || result.Type == DieType.Fire ||
                                    result.Type == DieType.Ice || result.Type == DieType.Nature;
        AddLine(PoolRowKey.FromDieType(DieType.Damage), result.TotalDamageContribution, GameIconCatalog.GetElementIcon(DieType.Damage), damageIsEnemyTargeted);
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
                var enemyTargeted = IsEnemyTargetedPoolContribution(extra);
                var attackAll = result.AttackAllEnemies && enemyTargeted;
                lines.Add(new RollOutcomeVisualLine
                {
                    RowKey = extra.PoolKey,
                    Amount = extra.Amount,
                    IconOverride = extra.Icon,
                    BackgroundOverride = rowBg,
                    IsVisualFlyoutOnly = extra.VisualFlyoutOnly,
                    FlyToPlayerStatusBar = extra.FlyToPlayerStatusBar,
                    EnemyTargeted = enemyTargeted,
                    AttackAllEnemies = attackAll,
                    SourceAction = enemyTargeted ? extra.PoolSourceAction : null,
                    ResolvesImmediatelyOnDrop = enemyTargeted &&
                                                extra.PoolSourceAction != null &&
                                                extra.PoolSourceAction.TriggerImmediatelyOnDrop
                });
            }
        }

        if (kineticArmorThisRoll)
            AddLine(PoolRowKey.FromDieType(DieType.Armor), 1, GameIconCatalog.GetElementIcon(DieType.Armor));

        if (includeParkedRerollLine)
            TryAppendCollapsedParkedRerollLine(result.Face, lines);

        if (includeParkedRerollLine)
            TryAppendIncreaseOtherElementsVisualLine(result.Face, lines);

        return lines;
    }

    private static void TryAppendIncreaseOtherElementsVisualLine(DieFaceSO face, List<RollOutcomeVisualLine> lines)
    {
        if (face?.actions == null)
            return;

        IncreaseOtherElementsAction increase = null;
        foreach (var action in face.actions)
        {
            if (action is IncreaseOtherElementsAction inc)
            {
                increase = inc;
                break;
            }
        }

        if (increase == null)
            return;

        var icon = GameIconCatalog.GetActionIcon(ActionVisualId.IncreaseOtherElements);
        if (icon == null)
            return;

        lines.Add(new RollOutcomeVisualLine
        {
            RowKey = PoolRowKey.Custom(ActionVisualId.IncreaseOtherElements.ToString()),
            Amount = increase.BonusAmount,
            IconOverride = icon,
            BackgroundOverride = GameIconCatalog.GetActionBackground(ActionVisualId.IncreaseOtherElements),
            IsVisualFlyoutOnly = true,
            RemoveOnIncreaseOtherLaunch = true,
        });
    }

    /// <summary>One parked die-to-die action row per face (Reroll Other / Roll Again), not per action instance.</summary>
    private static void TryAppendCollapsedParkedRerollLine(DieFaceSO face, List<RollOutcomeVisualLine> lines)
    {
        if (!TryResolveParkedDieToDieActionVisual(face, out var visualId))
            return;

        var icon = GameIconCatalog.GetActionIcon(visualId);
        if (icon == null)
            return;

        lines.Add(new RollOutcomeVisualLine
        {
            RowKey = PoolRowKey.Custom(visualId.ToString()),
            Amount = 0,
            IconOverride = icon,
            BackgroundOverride = GameIconCatalog.GetActionBackground(visualId),
            ParkUntilDieToDieReroll = true,
            IsVisualFlyoutOnly = true,
        });
    }

    private static bool TryResolveParkedDieToDieActionVisual(DieFaceSO face, out ActionVisualId visualId)
    {
        visualId = default;
        if (face?.actions == null)
            return false;

        var hasRerollOther = false;
        var hasTriggeringReroll = false;

        foreach (var a in face.actions)
        {
            if (a is RerollOtherDiceAfterAllSettledAction)
                hasRerollOther = true;
            if (a is RerollDieAction reroll && reroll.Scope == RerollDieAction.RerollDieScope.RerollTriggeringDieOnly)
                hasTriggeringReroll = true;
        }

        if (hasRerollOther)
        {
            visualId = ActionVisualId.RerollOtherDice;
            return true;
        }

        if (hasTriggeringReroll)
        {
            visualId = ActionVisualId.RerollDie;
            return true;
        }

        return false;
    }

    /// <summary>True when a deferred pool row applies an enemy-target status (e.g. enemy Burn) and so must be assigned to an enemy.</summary>
    private static bool IsEnemyTargetedPoolContribution(FacePoolExtraContribution extra)
    {
        return extra.PoolSourceAction != null &&
               extra.PoolSourceAction.StatusEffectDefinition != null &&
               extra.PoolSourceAction.StatusEffectDefinition.target == StatusEffectTarget.Enemy;
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

    private static bool IsEnemyTargetedAction(IGameAction action) =>
        action is ApplyStatusEffectAction apply &&
        apply.StatusEffectDefinition != null &&
        apply.StatusEffectDefinition.target == StatusEffectTarget.Enemy;

    private void ExecuteActionForFaceTargets(FaceResult face, IGameAction action, GameActionContext faceCtx, Action<GameActionContext> execute)
    {
        if (face != null && face.AttackAllEnemies && IsEnemyTargetedAction(action))
        {
            for (var i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                faceCtx.Enemy = enemy;
                execute(faceCtx);
            }

            return;
        }

        faceCtx.Enemy = ResolveActionTargetEnemy(face, action);
        execute(faceCtx);
    }

    /// <summary>
    /// Per-piece targeting: the enemy a specific deferred action resolves against. Enemy-targeted statuses use their drag-assigned
    /// enemy (or the primary enemy when unassigned / dead); everything else defaults to the Main Enemy.
    /// </summary>
    private EnemyController ResolveActionTargetEnemy(FaceResult face, IGameAction action)
    {
        if (face != null && action is ApplyStatusEffectAction apply &&
            apply.StatusEffectDefinition != null &&
            apply.StatusEffectDefinition.target == StatusEffectTarget.Enemy)
        {
            var assigned = face.GetActionTarget(action);
            if (assigned != null && assigned.IsAlive)
                return assigned;
            return ResolvePrimaryTargetEnemy();
        }

        return activeEnemy;
    }

    public GameActionContext BuildEnemyActionContext(EnemyActionSO sourceIntent) =>
        BuildEnemyActionContext(sourceIntent, activeEnemy);

    public GameActionContext BuildEnemyActionContext(EnemyActionSO sourceIntent, EnemyController actingEnemy)
    {
        return new GameActionContext
        {
            CombatManager = this,
            Player = player,
            Enemy = actingEnemy != null ? actingEnemy : activeEnemy,
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

    /// <summary>Status context targeting a specific enemy (multi-enemy turns / per-target resolution).</summary>
    private StatusEffectContext BuildStatusContext(EnemyController enemy) =>
        new StatusEffectContext { CombatManager = this, Player = player, Enemy = enemy != null ? enemy : activeEnemy };

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
    public void ApplySingleEnemyPhysicalHitFromIntent(EnemyActionSO action) =>
        ApplySingleEnemyPhysicalHitFromIntent(action, activeEnemy);

    public void ApplySingleEnemyPhysicalHitFromIntent(EnemyActionSO action, EnemyController actingEnemy)
    {
        var enemy = actingEnemy != null ? actingEnemy : activeEnemy;
        if (action == null || action.damage <= 0 || enemy == null || player == null)
            return;

        ApplyEnemyPhysicalHitToPlayer(action.damage, enemy, healActingEnemyForUnblockedPlayerDamage: false);
    }

    /// <summary>Physical hit from an enemy game action; heals the acting enemy for unblocked player HP damage.</summary>
    public void ApplyEnemyPhysicalLeechHit(int baseDamage, EnemyController actingEnemy) =>
        ApplyEnemyPhysicalHitToPlayer(baseDamage, actingEnemy, healActingEnemyForUnblockedPlayerDamage: true);

    /// <summary>Spawned-add support: armor, heal, and/or status applied to <see cref="MainEnemy"/>.</summary>
    public void ApplyBenefitToMainEnemy(int armor, int heal, StatusEffectSO statusEffect, int statusStacks)
    {
        var main = MainEnemy;
        if (main == null || !main.IsAlive)
            return;

        if (armor > 0)
            main.AddArmor(armor);

        if (heal > 0)
            main.Heal(heal);

        if (statusEffect != null && statusStacks > 0)
        {
            var statusCtx = BuildStatusContext(main);
            main.StatusEffects.ApplyStatus(statusEffect, statusStacks, statusCtx);
        }
    }

    void ApplyEnemyPhysicalHitToPlayer(int baseDamage, EnemyController actingEnemy, bool healActingEnemyForUnblockedPlayerDamage)
    {
        var enemy = actingEnemy != null ? actingEnemy : activeEnemy;
        if (baseDamage <= 0 || enemy == null || player == null)
            return;

        var statusCtx = BuildStatusContext(enemy);
        var boosted = baseDamage + enemy.StatusEffects.GetTotalPerDieAttackDamageBonus(statusCtx);
        var damage = enemy.StatusEffects.ModifyEnemyHitDamage(statusCtx, boosted);
        if (enemy.StatusEffects.CheckRedirectAttackToSelf(statusCtx))
            enemy.TakeDamage(damage);
        else
        {
            var hadImmune = player.StatusEffects.GetStacks<ImmuneEffectSO>() > 0;
            if (hadImmune)
                damage = Mathf.Min(damage, 1);
            var playerHpBefore = player.GetCurrentHealth();
            player.TakeDamage(damage, PlayerDamageSource.EnemyPhysicalAttack);
            if (healActingEnemyForUnblockedPlayerDamage)
            {
                var healthLoss = playerHpBefore - player.GetCurrentHealth();
                if (healthLoss > 0)
                    enemy.Heal(healthLoss);
            }
            if (hadImmune)
                player.StatusEffects.ConsumeImmuneStackAfterHit(statusCtx);
            var thornsRetaliate = player.StatusEffects.GetThornsRetaliateStacks();
            if (thornsRetaliate > 0)
                enemy.TakeDamage(thornsRetaliate);
        }
    }

    public void ApplyEnemyArmorFromIntent(EnemyActionSO action) =>
        ApplyEnemyArmorFromIntent(action, activeEnemy);

    public void ApplyEnemyArmorFromIntent(EnemyActionSO action, EnemyController actingEnemy)
    {
        var enemy = actingEnemy != null ? actingEnemy : activeEnemy;
        if (action != null && action.armor > 0 && enemy != null)
            enemy.AddArmor(action.armor);
    }

    public void ExecuteEnemyIntentGameActionAtIndex(EnemyActionSO action, int actionListIndex) =>
        ExecuteEnemyIntentGameActionAtIndex(action, actionListIndex, activeEnemy);

    public void ExecuteEnemyIntentGameActionAtIndex(EnemyActionSO action, int actionListIndex, EnemyController actingEnemy)
    {
        if (action?.actions == null || actionListIndex < 0 || actionListIndex >= action.actions.Count || player == null)
            return;

        var gameAction = action.actions[actionListIndex];
        if (gameAction == null || gameAction is FaceResolveModifierBase)
            return;

        var actionCtx = BuildEnemyActionContext(action, actingEnemy);
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
                }
                ProcessPrecisionQueue();
            });
        }
        else
            CheckBustStatusAndOpenFlyoutGate();
    }

    private void CheckBustStatusAndOpenFlyoutGate()
    {
        if (HasPendingDeferredRerolls())
            return;

        CheckBustStatus();
        _postRaiseCombatGateOpen = true;
    }

    private bool QualifiesForPerfectCast()
    {
        if (currentPower == maxPower)
            return true;
        if (maxPower > 1 && currentPower == maxPower - 1 &&
            RelicActionRunner.QueryBoolOr(RelicPhases.QueryPerfectAtMaxMinusOne, this))
            return true;
        if (currentPower == maxPower + 1 &&
            RelicActionRunner.QueryBoolOr(RelicPhases.QueryPerfectAtMaxPlusOne, this))
            return true;
        return false;
    }

    private void CheckBustStatus()
    {
        if (QualifiesForPerfectCast())
        {
            ProgressionEventBridge.NotifyPerfectCast();
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

            // Multi-enemy: scale every on-board enemy element layout + the still-unassigned drag tokens.
            for (var i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null) continue;
                enemy.StatusEffects.TickPerfectStrike(BuildStatusContext(enemy));
                if (enemy.AssignedElementPool != null)
                    enemy.AssignedElementPool.MultiplyAllDisplayed(appliedMultiplier, refreshIcons: false);
            }

            if (targetAssignment != null)
                targetAssignment.MultiplyPendingTokenAmounts(appliedMultiplier);

            RelicActionRunner.RunPhase(this, RelicPhases.OnPerfectStrike);
            var poolsAfter = SnapshotStoredActionsPool();
            if (CheckVictory())
            {
                NotifyAllStoredActionsPoolUI();
                return;
            }

            // Reorder: multiply + play the Perfect Cast sequence first, THEN wait for the player to attach the
            // rolled outcomes to enemies, THEN continue to the hit-fx fly (SubmitTurn).
            if (jackpotPresentation != null)
                StartCoroutine(CoJackpotAfterFlyoutsThenPresentation(jackpotMultiplier, poolsBefore, poolsAfter));
            else
            {
                StartCoroutine(CoAfterRollVisualsThen(() =>
                {
                    NotifyAllStoredActionsPoolUI();
                    RunTargetAssignmentGate(SubmitTurn);
                }));
            }
        }
        else if (currentPower > maxPower)
        {
            if (RelicActionRunner.TryConsumeFreeBust(this))
            {
                targetAssignment?.CancelPendingAssignments();
                SubmitTurn();
                return;
            }

            if (bustProtected)
            {
                targetAssignment?.CancelPendingAssignments();
                SubmitTurn();
                return;
            }

            if (_turnRegistry.SupernovaBustOverrideActive)
            {
                var supernovaTarget = ResolvePrimaryTargetEnemy();
                if (supernovaTarget != null && _turnRegistry.SupernovaBustDamage > 0)
                    supernovaTarget.TakeDamage(_turnRegistry.SupernovaBustDamage);
                _turnRegistry.SupernovaBustOverrideActive = false;
                if (CheckVictory()) return;
                targetAssignment?.CancelPendingAssignments();
                SubmitTurn();
                return;
            }

            ProgressionEventBridge.NotifyCastOverload();
            AbortDeferredPostSubmitRerolls();
            targetAssignment?.CancelPendingAssignments(playBustDestroyVisual: true);
            _skipFlyoutFlyPhaseThisBatch = true;
            ChangeState(CombatState.BustCheck);
            NotifyAllStoredActionsPoolUI();
            CombatEvents.OnBustOccurred?.Invoke(GetPendingAttack(), GetPendingDefense());
        }
        else
        {
            StartCoroutine(CoAfterRollVisualsThen(() =>
            {
                RunTargetAssignmentGate(() =>
                {
                    if (rollsRemaining <= 0) SubmitTurn();
                    else ChangeState(CombatState.WaitingForRoll);
                });
            }));
        }
    }

    /// <summary>
    /// Multi-enemy: blocks the turn until the player assigns every rolled enemy-targeted outcome to an enemy.
    /// Single-enemy fights auto-assign to the lone enemy (no drag) so existing combat plays unchanged.
    /// </summary>
    private void RunTargetAssignmentGate(Action onComplete)
    {
        var primary = ResolvePrimaryTargetEnemy();
        if (!IsMultiEnemy || targetAssignment == null || !targetAssignment.HasPendingAssignments)
        {
            AutoAssignUnassignedEnemyFacesTo(primary);
            targetAssignment?.CancelPendingAssignments();
            onComplete?.Invoke();
            return;
        }

        ChangeState(CombatState.AwaitingTargetAssignment);
        targetAssignment.BeginGate(onComplete);
    }

    /// <summary>
    /// End Turn from UI must run the same perfect / bust pipeline as <see cref="ProcessPrecisionQueue"/> —
    /// otherwise <see cref="appliedMultiplier"/> never applies and turn-end heals (etc.) stay at ×1.
    /// </summary>
    private void ManualEndTurn()
    {
        if (currentState != CombatState.WaitingForRoll) return;

        if (HasPendingDeferredRerolls())
            return;

        if (QualifiesForPerfectCast() || currentPower > maxPower)
        {
            CheckBustStatusAndOpenFlyoutGate();
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
        CheckBustStatusAndOpenFlyoutGate();
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
    }

    /// <summary>Dissolve every 3D die still on the table after Cast Overload bust visuals have activated on pool icons.</summary>
    public void DissolveAllDiceAfterBustIconsPresented()
    {
        if (spawner != null)
            spawner.ClearOldDice();
    }

    /// <summary>Empties every stored-actions pool UI row after Cast Overload presentation (called by <see cref="BustPresentationController"/>).</summary>
    public void ClearStoredActionPoolUiAfterBust()
    {
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            if (_activeEnemies[i] != null && _activeEnemies[i].AssignedElementPool != null)
                _activeEnemies[i].AssignedElementPool.ClearAllRows();
        }

        targetAssignment?.CancelPendingAssignments();
        NotifyAllStoredActionsPoolUI();
    }

    /// <summary>Resumes the turn after bust pool UI has finished presenting.</summary>
    public void ContinueTurnAfterBustPresentation()
    {
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

        var impactedEnemies = CollectEnemiesWithPlayerTurnImpact();
        var mainEnemy = ResolvePrimaryTargetEnemy();
        var orbTargetEnemy = mainEnemy;
        var statusCtx = BuildStatusContext(orbTargetEnemy);
        int pendingAttack = GetPendingAttack();
        pendingAttack += player.StatusEffects.GetTotalBonusAttack(statusCtx);
        if (orbTargetEnemy != null)
            pendingAttack = orbTargetEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, pendingAttack);
        int pendingDefense = GetPendingDefense();

        bool mainHasPlayerImpact = mainEnemy != null && impactedEnemies.Contains(mainEnemy);
        bool hasEnemyImpact = impactedEnemies.Count > 0;
        bool enemyDamageLine = hasEnemyImpact || _turnRegistry.BurnAppliedThisTurn > 0;
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
            bool flyOrbToEnemy = enemyDamageLine && hasEnemyImpact;
            bool flyMainOrb = flyOrbToEnemy && mainHasPlayerImpact;
            bool duplicateOnlyFlight = flyOrbToEnemy && !flyMainOrb;
            Transform orbAnchor = flyMainOrb
                ? mainEnemy.GetPowerOrbHitAnchor()
                : flyOrbToEnemy
                    ? impactedEnemies[0].GetPowerOrbHitAnchor()
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
                    flyMainOrb,
                    duplicateOnlyFlight,
                    impactedEnemies,
                    allowZeroCombatPower,
                    forceStartingVisibleScale,
                    mainEnemy));
            }
        }
    }

    private IEnumerator CoSubmitTurnAfterOrbFlight(
        int pendingAttack,
        int pendingDefense,
        Transform orbAnchor,
        bool flyMainOrb,
        bool duplicateOnlyFlight,
        IReadOnlyList<EnemyController> impactedEnemies,
        bool allowZeroCombatPower,
        bool forceStartingVisibleScale,
        EnemyController mainEnemy)
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
                PowerOrbImpactTarget.Enemy,
                orbAnchor.position,
                flyMainOrb ? mainEnemy : null));
        }

        void AnnounceDuplicateOrbImpact(Transform anchor, EnemyController enemy)
        {
            if (anchor == null || enemy == null) return;
            CombatEvents.OnPowerOrbImpact?.Invoke(new PowerOrbImpactPayload(
                PowerOrbImpactTarget.Enemy,
                anchor.position,
                enemy));
        }

        void OnOrbImpact()
        {
            if (!duplicateOnlyFlight)
                AnnounceOrbImpact();
            if (attackResolved) return;
            attackResolved = true;
            continueCombat = RunPlayerPhysicalResolution(pendingAttack);
        }

        var duplicateTargets = BuildDuplicateOrbFlightTargets(flyMainOrb, duplicateOnlyFlight, impactedEnemies, mainEnemy);

        if (duplicateOnlyFlight && duplicateTargets.Count > 0)
        {
            var duplicateAnchors = new List<Transform>();
            var duplicateEnemies = new List<EnemyController>();
            if (TryBuildOrbFlightAnchors(duplicateTargets, duplicateAnchors, duplicateEnemies))
            {
                yield return powerOrbVisual.CoDuplicateFlightsToAnchors(
                    duplicateAnchors,
                    forceStartingVisibleScale,
                    anchor => AnnounceDuplicateOrbImpact(anchor, ResolveEnemyForOrbAnchor(anchor, duplicateAnchors, duplicateEnemies)));
            }
        }
        else if (flyMainOrb)
        {
            if (IsMultiEnemy && duplicateTargets.Count > 0)
                BeginDuplicateOrbFlightsToTargets(duplicateTargets, forceStartingVisibleScale);

            IEnumerator flight = powerOrbVisual.RunFlightToWorldAnchor(
                orbAnchor, allowZeroCombatPower, forceStartingVisibleScale, OnOrbImpact);
            while (flight.MoveNext())
                yield return flight.Current;
        }
        else
        {
            void AnnounceSupportOrbImpact()
            {
                if (impactAnnounced) return;
                impactAnnounced = true;
                CombatEvents.OnPowerOrbImpact?.Invoke(new PowerOrbImpactPayload(
                    PowerOrbImpactTarget.PlayerSupport,
                    orbAnchor.position,
                    null));
            }

            void OnSupportOrbImpact()
            {
                AnnounceSupportOrbImpact();
                if (attackResolved) return;
                attackResolved = true;
                continueCombat = RunPlayerPhysicalResolution(pendingAttack);
            }

            IEnumerator flight = powerOrbVisual.RunFlightToWorldAnchor(
                orbAnchor, allowZeroCombatPower, forceStartingVisibleScale, OnSupportOrbImpact);
            while (flight.MoveNext())
                yield return flight.Current;
        }

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
        ClearAllEnemyPools();
        return true;
    }

    /// <summary>Empties every per-enemy element layout (the accumulated outcomes have just resolved / flown).</summary>
    private void ClearAllEnemyPools()
    {
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            if (_activeEnemies[i] != null && _activeEnemies[i].AssignedElementPool != null)
                _activeEnemies[i].AssignedElementPool.ClearAllRows();
        }
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

                // Per-piece targeting: enemy statuses resolve against assigned enemy(ies).
                ExecuteActionForFaceTargets(face, a, faceCtx, ctx => a.Execute(ctx));
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
            ExecuteActionForFaceTargets(p.SourceFace, p.Action, faceCtx, ctx => p.Action.Execute(ctx));
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
        var globalPhysicalBonus = Mathf.Max(0, bonusDamageFromActions + playerBonusAttack);

        // Group rolled element damage per assigned enemy. Faces left unassigned (or with a dead target) fall back to the primary enemy.
        var fallback = ResolvePrimaryTargetEnemy();
        var perEnemy = new Dictionary<EnemyController, ElementDamageTotals>();

        ElementDamageTotals TotalsFor(EnemyController enemy)
        {
            if (enemy == null) enemy = fallback;
            if (enemy == null) return default;
            if (!perEnemy.TryGetValue(enemy, out var t))
                t = new ElementDamageTotals();
            return t;
        }

        void Store(EnemyController enemy, ElementDamageTotals t)
        {
            if (enemy == null) enemy = fallback;
            if (enemy == null) return;
            perEnemy[enemy] = t;
        }

        // Flat bonus damage (player strength / action bonuses) goes to the primary enemy.
        if (globalPhysicalBonus > 0 && fallback != null)
        {
            var t = TotalsFor(fallback);
            t.Physical += globalPhysicalBonus;
            Store(fallback, t);
        }

        for (var i = 0; i < channeledFaces.Count; i++)
        {
            var face = channeledFaces[i];
            if (face == null || face.Damage <= 0)
                continue;

            if (face.AttackAllEnemies)
            {
                for (var e = 0; e < _activeEnemies.Count; e++)
                {
                    var multiTarget = _activeEnemies[e];
                    if (multiTarget == null || !multiTarget.IsAlive) continue;
                    AddFaceElementDamageToTotals(face, multiTarget, fallback, TotalsFor, Store);
                }

                continue;
            }

            var target = face.DamageTargetEnemy != null && face.DamageTargetEnemy.IsAlive ? face.DamageTargetEnemy : fallback;
            if (target == null) continue;
            AddFaceElementDamageToTotals(face, target, fallback, TotalsFor, Store);
        }

        var grandPhysical = 0;
        var grandFire = 0;
        foreach (var kvp in perEnemy)
        {
            var enemy = kvp.Key;
            if (enemy == null || !enemy.IsAlive) continue;
            var enemyCtx = BuildStatusContext(enemy);
            var totals = kvp.Value;

            var physical = enemy.StatusEffects.ApplyDamageModifiers(enemyCtx, totals.Physical);
            var fire = enemy.StatusEffects.ApplyDamageModifiers(enemyCtx, totals.Fire);
            var ice = enemy.StatusEffects.ApplyDamageModifiers(enemyCtx, totals.Ice);
            var nature = enemy.StatusEffects.ApplyDamageModifiers(enemyCtx, totals.Nature);

            if (physical > 0)
            {
                enemy.TakeDamage(physical, DieType.Damage, EnemyDamagePresentationKind.Physical);
                grandPhysical += physical;
            }

            if (fire > 0)
            {
                enemy.TakeDamage(fire, DieType.Fire, EnemyDamagePresentationKind.Physical);
                grandFire += fire;
            }

            if (ice > 0)
                enemy.TakeDamage(ice, DieType.Ice, EnemyDamagePresentationKind.Physical);
            if (nature > 0)
                enemy.TakeDamage(nature, DieType.Nature, EnemyDamagePresentationKind.Physical);

            var retaliate = enemy.StatusEffects.GetThornsRetaliateStacks();
            if (retaliate > 0 && player != null)
            {
                var thornsPopupAnchor = Vector3.Lerp(player.GetDamageNumberWorldPosition(), enemy.GetDamageNumberWorldPosition(), 0.25f);
                player.TakeDamage(retaliate, PlayerDamageSource.ThornsRetaliation, thornsPopupAnchor);
                if (CheckDefeat()) return false;
            }
        }

        if (grandPhysical > 0)
            ProgressionEventBridge.NotifyPhysicalDamageDealt(grandPhysical);
        if (grandFire > 0)
            ProgressionEventBridge.NotifyFireDamageDealt(grandFire);

        if (CheckVictory()) return false;
        return true;
    }

    private struct ElementDamageTotals
    {
        public int Physical;
        public int Fire;
        public int Ice;
        public int Nature;
    }

    private static void AddFaceElementDamageToTotals(
        FaceResult face,
        EnemyController target,
        EnemyController fallback,
        Func<EnemyController, ElementDamageTotals> totalsFor,
        Action<EnemyController, ElementDamageTotals> store)
    {
        if (face == null || target == null)
            return;

        var t = totalsFor(target);
        switch (face.Type)
        {
            case DieType.Damage:
                t.Physical += face.TotalDamageContribution;
                break;
            case DieType.Fire:
                t.Fire += face.Damage;
                break;
            case DieType.Ice:
                t.Ice += face.Damage;
                break;
            case DieType.Nature:
                t.Nature += face.Damage;
                break;
        }

        store(target, t);
    }

    /// <summary>
    /// Drag-to-assign: routes a single rolled outcome <b>piece</b> (the face's damage, or one enemy-targeted action) to the chosen
    /// enemy. The damage piece (sourceAction == null) sets <see cref="FaceResult.DamageTargetEnemy"/>; an action piece sets its
    /// per-action target. Accumulates under that enemy's element layout, or resolves instantly when the piece is Trigger-Immediately.
    /// </summary>
    public void AssignRolledOutcomePieceToEnemy(FaceResult face, ApplyStatusEffectAction sourceAction, EnemyController enemy,
        RollOutcomeVisualLine line, bool resolveImmediately)
    {
        if (face == null || enemy == null)
            return;

        if (sourceAction == null && face.AttackAllEnemies)
        {
            var allTargetPool = enemy.AssignedElementPool;
            if (allTargetPool != null)
                allTargetPool.ApplyPoolDelta(line.RowKey, line.Amount, line.IconOverride, line.BackgroundOverride);
            NotifyStoredActionsPoolUpdated();
            return;
        }

        if (sourceAction == null)
            face.DamageTargetEnemy = enemy;
        else
            face.SetActionTarget(sourceAction, enemy);

        // Only action pieces can be Trigger-Immediately (the damage piece always accumulates to turn end).
        if (resolveImmediately && sourceAction != null)
        {
            ResolveImmediateActionOnDrop(face, sourceAction, enemy, consumeFromFace: !face.AttackAllEnemies);
            return;
        }

        var pool = enemy.AssignedElementPool;
        if (pool != null)
            pool.ApplyPoolDelta(line.RowKey, line.Amount, line.IconOverride, line.BackgroundOverride);

        NotifyStoredActionsPoolUpdated();
    }

    /// <summary>Single-enemy fights (no drag): point every still-unassigned enemy-targeted piece at the lone living enemy.</summary>
    private void AutoAssignUnassignedEnemyFacesTo(EnemyController enemy)
    {
        if (enemy == null) return;
        for (var i = 0; i < channeledFaces.Count; i++)
        {
            var face = channeledFaces[i];
            if (face == null) continue;
            if (face.AttackAllEnemies)
                continue;

            if (face.HasEnemyDamagePiece && face.DamageTargetEnemy == null)
                face.DamageTargetEnemy = enemy;

            if (face.Actions == null) continue;
            foreach (var a in face.Actions)
            {
                if (a is ApplyStatusEffectAction apply &&
                    apply.StatusEffectDefinition != null &&
                    apply.StatusEffectDefinition.target == StatusEffectTarget.Enemy &&
                    face.GetActionTarget(a) == null)
                    face.SetActionTarget(a, enemy);
            }
        }
    }

    /// <summary>Removes a Trigger-Immediately action from the face after it has resolved on every attack-all target.</summary>
    public void ConsumeImmediateActionAfterAttackAllAssign(FaceResult face, ApplyStatusEffectAction action)
    {
        if (face == null || action == null || !face.AttackAllEnemies)
            return;

        ResolveImmediateActionOnDrop(face, action, enemy: null, consumeFromFace: true);
    }

    /// <summary>Resolves a single enemy-targeted action (e.g. Burn) against an enemy the instant its token is dropped (Trigger Immediately).</summary>
    private void ResolveImmediateActionOnDrop(FaceResult face, ApplyStatusEffectAction action, EnemyController enemy, bool consumeFromFace)
    {
        if (face == null || action == null)
            return;

        if (enemy != null)
        {
            if (!enemy.IsAlive)
                return;

            var actionCtx = BuildContext(face);
            actionCtx.Enemy = enemy;
            actionCtx.PendingApplyStackOverrides = BuildPendingApplyStackOverrides(face);
            action.Execute(actionCtx);
            CheckVictory();
        }

        if (!consumeFromFace)
            return;

        face.Actions.Remove(action);
        for (var i = face.ActionPoolContributions.Count - 1; i >= 0; i--)
        {
            if (face.ActionPoolContributions[i].PoolSourceAction == action)
                face.ActionPoolContributions.RemoveAt(i);
        }

        NotifyStoredActionsPoolUpdated();
    }

    private void BeginDuplicateOrbFlightsToOtherTargets(EnemyController primaryOrbTarget, bool forceStartingVisibleScale)
    {
        if (primaryOrbTarget == null)
            return;

        var targets = new List<EnemyController>();
        foreach (var enemy in EnumerateAssignedDamageTargets())
        {
            if (enemy != null && enemy.IsAlive && enemy != primaryOrbTarget)
                targets.Add(enemy);
        }

        BeginDuplicateOrbFlightsToTargets(targets, forceStartingVisibleScale);
    }

    private void BeginDuplicateOrbFlightsToTargets(
        IReadOnlyList<EnemyController> targets,
        bool forceStartingVisibleScale)
    {
        if (powerOrbVisual == null || targets == null || targets.Count == 0)
            return;

        var duplicateAnchors = new List<Transform>();
        var duplicateEnemies = new List<EnemyController>();
        if (!TryBuildOrbFlightAnchors(targets, duplicateAnchors, duplicateEnemies))
            return;

        powerOrbVisual.BeginDuplicateFlights(duplicateAnchors, forceStartingVisibleScale, anchor =>
        {
            AnnounceDuplicateOrbImpactForAnchor(anchor, duplicateAnchors, duplicateEnemies);
        });
    }

    private static void AnnounceDuplicateOrbImpactForAnchor(
        Transform anchor,
        IReadOnlyList<Transform> anchors,
        IReadOnlyList<EnemyController> enemies)
    {
        var enemy = ResolveEnemyForOrbAnchor(anchor, anchors, enemies);
        if (enemy == null)
            return;

        CombatEvents.OnPowerOrbImpact?.Invoke(new PowerOrbImpactPayload(
            PowerOrbImpactTarget.Enemy,
            anchor.position,
            enemy));
    }

    private static EnemyController ResolveEnemyForOrbAnchor(
        Transform anchor,
        IReadOnlyList<Transform> anchors,
        IReadOnlyList<EnemyController> enemies)
    {
        if (anchor == null || anchors == null || enemies == null)
            return null;

        for (var i = 0; i < anchors.Count; i++)
        {
            if (anchors[i] == anchor)
                return enemies[i];
        }

        return null;
    }

    private bool TryBuildOrbFlightAnchors(
        IReadOnlyList<EnemyController> enemies,
        List<Transform> anchors,
        List<EnemyController> orderedEnemies)
    {
        anchors.Clear();
        orderedEnemies.Clear();

        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
                continue;

            var anchor = enemy.GetPowerOrbHitAnchor();
            if (anchor == null)
            {
                Debug.LogError(
                    $"CombatManager: enemy '{enemy.name}' has no power-orb hit anchor — skipping duplicate hit FX flight.",
                    enemy);
                continue;
            }

            orderedEnemies.Add(enemy);
            anchors.Add(anchor);
        }

        return anchors.Count > 0;
    }

    private List<EnemyController> BuildDuplicateOrbFlightTargets(
        bool flyMainOrb,
        bool duplicateOnlyFlight,
        IReadOnlyList<EnemyController> impactedEnemies,
        EnemyController mainEnemy)
    {
        var targets = new List<EnemyController>();
        if (impactedEnemies == null || impactedEnemies.Count == 0)
            return targets;

        if (duplicateOnlyFlight)
        {
            for (var i = 0; i < impactedEnemies.Count; i++)
            {
                var enemy = impactedEnemies[i];
                if (enemy != null && enemy.IsAlive)
                    targets.Add(enemy);
            }

            return targets;
        }

        if (!flyMainOrb || mainEnemy == null)
            return targets;

        for (var i = 0; i < impactedEnemies.Count; i++)
        {
            var enemy = impactedEnemies[i];
            if (enemy != null && enemy.IsAlive && enemy != mainEnemy)
                targets.Add(enemy);
        }

        return targets;
    }

    /// <summary>
    /// Enemies that should receive a power-orb hit (main or duplicate flight). Includes drag-assigned damage targets and any
    /// multi-enemy layout with ≥1 displayed pool row (covers Attack All Enemies deposits that never set DamageTargetEnemy).
    /// </summary>
    private IEnumerable<EnemyController> EnumerateAssignedDamageTargets()
    {
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            var enemy = _activeEnemies[i];
            if (enemy != null && enemy.IsAlive && EnemyHasPlayerTurnImpact(enemy))
                yield return enemy;
        }
    }

    private List<EnemyController> CollectEnemiesWithPlayerTurnImpact()
    {
        var result = new List<EnemyController>();
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            var enemy = _activeEnemies[i];
            if (enemy != null && enemy.IsAlive && EnemyHasPlayerTurnImpact(enemy))
                result.Add(enemy);
        }

        return result;
    }

    /// <summary>
    /// True when the player assigned damage, debuffs, or other turn-end enemy effects to this enemy (not merely because they are the Main Enemy).
    /// </summary>
    private bool EnemyHasPlayerTurnImpact(EnemyController enemy)
    {
        if (enemy == null || !enemy.IsAlive)
            return false;

        var pool = enemy.AssignedElementPool;
        if (pool != null && pool.HasAnyDisplayedElements())
            return true;

        var fallback = ResolvePrimaryTargetEnemy();
        if (enemy == fallback)
        {
            var statusCtx = BuildStatusContext(enemy);
            var playerBonus = player != null ? player.StatusEffects.GetTotalBonusAttack(statusCtx) : 0;
            var globalPhysicalBonus = Mathf.Max(0, bonusDamageFromActions + playerBonus);
            if (globalPhysicalBonus > 0)
                return true;
        }

        for (var i = 0; i < channeledFaces.Count; i++)
        {
            var face = channeledFaces[i];
            if (face == null)
                continue;

            if (face.AttackAllEnemies)
            {
                if (FaceHasPlayerImpactOnEnemies(face))
                    return true;
                continue;
            }

            if (face.Damage > 0)
            {
                var target = face.DamageTargetEnemy != null && face.DamageTargetEnemy.IsAlive
                    ? face.DamageTargetEnemy
                    : fallback;
                if (target == enemy)
                    return true;
            }

            if (face.Actions == null)
                continue;

            foreach (var action in face.Actions)
            {
                if (!IsEnemyTargetedAction(action))
                    continue;

                var target = ResolveActionTargetEnemy(face, action);
                if (target == enemy)
                    return true;
            }
        }

        for (var i = 0; i < _pendingAfterPhysicalApplyStatuses.Count; i++)
        {
            var pending = _pendingAfterPhysicalApplyStatuses[i];
            if (pending.SourceFace == null || pending.Action == null)
                continue;

            if (pending.SourceFace.AttackAllEnemies)
                return true;

            var target = ResolveActionTargetEnemy(pending.SourceFace, pending.Action);
            if (target == enemy)
                return true;
        }

        return false;
    }

    private static bool FaceHasPlayerImpactOnEnemies(FaceResult face)
    {
        if (face.HasEnemyDamagePiece)
            return true;

        if (face.Actions == null)
            return false;

        foreach (var action in face.Actions)
        {
            if (IsEnemyTargetedAction(action))
                return true;
        }

        return false;
    }

    /// <summary>The default enemy used for unassigned / fallback player damage: the Main Enemy when alive, otherwise the first living enemy.</summary>
    public EnemyController ResolvePrimaryTargetEnemy()
    {
        if (activeEnemy != null && activeEnemy.IsAlive)
            return activeEnemy;
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            if (_activeEnemies[i] != null && _activeEnemies[i].IsAlive)
                return _activeEnemies[i];
        }

        return activeEnemy;
    }

    /// <summary>
    /// After all player-turn damage to the enemy: stepped <see cref="StatusEffectManager.TickTurnStartStepped"/> (optional delays),
    /// then <see cref="EnemyController.ResetArmor"/> before the enemy-turn coroutine.
    /// </summary>
    private IEnumerator CoResolveEnemyOpeningAndStartEnemyTurn()
    {
        if (player != null && _activeEnemies.Count > 0)
        {
            var opening = new List<EnemyController>(_activeEnemies);
            foreach (var enemy in opening)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                var openingCtx = BuildStatusContext(enemy);
                yield return StartCoroutine(enemy.StatusEffects.TickTurnStartStepped(
                    openingCtx,
                    delaySecondsBetweenPhysicalAndEachEnemyStatusTick,
                    delaySecondsBetweenPhysicalAndEachEnemyStatusTick));
            }

            if (CheckVictory()) yield break;

            foreach (var enemy in _activeEnemies)
            {
                if (enemy != null && enemy.IsAlive)
                    enemy.ResetArmor();
            }
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

    private IEnumerator CoJackpotAfterFlyoutsThenPresentation(int multiplier, Dictionary<PoolRowKey, int> poolsBefore, Dictionary<PoolRowKey, int> poolsAfter)
    {
        yield return new WaitUntil(() => pendingRollVisualSequences <= 0);
        yield return StartCoroutine(CoFinishJackpotAfterPresentation(multiplier, poolsBefore, poolsAfter));
    }

    private IEnumerator CoFinishJackpotAfterPresentation(int multiplier, Dictionary<PoolRowKey, int> poolsBefore, Dictionary<PoolRowKey, int> poolsAfter)
    {
        // Unity does not reliably run a nested IEnumerator with "yield return routine()"; must use StartCoroutine.
        yield return StartCoroutine(jackpotPresentation.Run(multiplier, poolsBefore, poolsAfter));
        NotifyAllStoredActionsPoolUI();
        // Perfect Cast reorder: only after the sequence does the player attach outcomes to enemies, then the hit-fx flies.
        RunTargetAssignmentGate(SubmitTurn);
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

                if (gameAction is LeechPhysicalDamageAction leech)
                {
                    var hits = Mathf.Max(1, leech.NumberOfAttacks);
                    for (var h = 0; h < hits; h++)
                    {
                        gameAction.Execute(actionCtx);
                        if (CheckDefeat())
                            yield break;
                        if (hits > 1 && h < hits - 1)
                            yield return new WaitForSeconds(0.4f);
                    }

                    continue;
                }

                gameAction.Execute(actionCtx);
            }

            if (CheckDefeat())
                yield break;
        }
    }

    private bool TryGetDelayBeforeNextActingEnemy(IReadOnlyList<EnemyController> actingEnemies, int currentIndex, out float delaySeconds)
    {
        delaySeconds = 0f;
        if (enemyTurnIntentSequence == null)
            return false;

        delaySeconds = enemyTurnIntentSequence.DelayBetweenEnemies;
        if (delaySeconds <= 0f)
            return false;

        for (var i = currentIndex + 1; i < actingEnemies.Count; i++)
        {
            var next = actingEnemies[i];
            if (next != null && next.IsAlive && next.IsActiveInRoster)
                return true;
        }

        return false;
    }

    private IEnumerator EnemyTurnRoutine()
    {
        _turnRegistry.ResetVolatile();

        if (enemyTurnIntroDelayAfterPlayerDamageSeconds > 0f)
            yield return new WaitForSeconds(enemyTurnIntroDelayAfterPlayerDamageSeconds);

        var enemyTurnIntroIsUp = false;
        var enemyTurnSpriteSortingApplied = false;
        if (enemyTurnIntroRoot != null)
        {
            if (player != null && _activeEnemies.Count > 0)
            {
                RefreshAllEnemyTurnSpriteDefaultMaterials();
                ApplyEnemyTurnSpriteSortingForIntentPhase(GetFirstActingEnemyForTurn());
                enemyTurnSpriteSortingApplied = true;
            }

            ChangeState(CombatState.EnemyTurnIntro);
            yield return CoEnemyTurnIntroShow();
            enemyTurnIntroIsUp = true;

            if (enemyTurnIntentSequence != null)
                yield return enemyTurnIntentSequence.CoWaitBeforeFirstAction();
        }

        ChangeState(CombatState.EnemyTurn);
        if (player != null && _activeEnemies.Count > 0)
        {
            if (!enemyTurnSpriteSortingApplied)
            {
                RefreshAllEnemyTurnSpriteDefaultMaterials();
                ApplyEnemyTurnSpriteSortingForIntentPhase(GetFirstActingEnemyForTurn());
                enemyTurnSpriteSortingApplied = true;
            }

            // Snapshot so spawned/defeated enemies during the turn don't corrupt iteration.
            var actingEnemies = new List<EnemyController>(_activeEnemies);
            for (var e = 0; e < actingEnemies.Count; e++)
            {
                var enemy = actingEnemies[e];
                if (enemy == null || !enemy.IsAlive || !enemy.IsActiveInRoster) continue;

                ApplyActiveEnemyTurnSpriteSorting(enemy);

                var statusCtx = BuildStatusContext(enemy);
                enemy.StatusEffects.TickBeforeEnemyTurn(statusCtx);
                if (CheckVictory())
                {
                    yield return CoEndEnemyTurnIndicatorAndRestoreSpriteSorting(enemyTurnIntroIsUp, enemyTurnSpriteSortingApplied);
                    yield break;
                }

                if (!enemy.IsAlive) continue;

                EnemyActionSO action = enemy.GetCurrentAction();
                yield return enemy.CoPresentEnemyTurnActionIntro(action);

                if (enemyTurnIntentSequence != null)
                    yield return enemyTurnIntentSequence.CoExecuteIntent(enemy, action, this);
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

                yield return enemy.CoPresentEnemyTurnActionOutro();

                if (enemy.IsAlive)
                    enemy.StatusEffects.TickAfterEnemyTurn(statusCtx);
                if (CheckDefeat())
                {
                    yield return CoEndEnemyTurnIndicatorAndRestoreSpriteSorting(enemyTurnIntroIsUp, enemyTurnSpriteSortingApplied);
                    yield break;
                }

                if (enemy.IsAlive)
                    enemy.PrepareNextAction();

                if (TryGetDelayBeforeNextActingEnemy(actingEnemies, e, out var delayBetweenEnemies))
                    yield return new WaitForSeconds(delayBetweenEnemies);
            }

            player.StatusEffects.TickAfterEnemyTurn(BuildStatusContext());
            if (CheckVictory() || CheckDefeat())
            {
                yield return CoEndEnemyTurnIndicatorAndRestoreSpriteSorting(enemyTurnIntroIsUp, enemyTurnSpriteSortingApplied);
                yield break;
            }
        }

        yield return CoEndEnemyTurnIndicatorAndRestoreSpriteSorting(enemyTurnIntroIsUp, enemyTurnSpriteSortingApplied);

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

        if (enemyTurnIntentSequence != null && enemyTurnIntentSequence.UsesTurnIndicatorAnimator)
        {
            enemyTurnIntroCanvasGroup.alpha = 1f;
            yield return enemyTurnIntentSequence.CoPresentTurnIndicatorIntro();
            yield break;
        }

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

    private IEnumerator CoEndEnemyTurnIndicatorAndRestoreSpriteSorting(bool introWasRaised, bool restoreSpriteSorting)
    {
        yield return CoTeardownEnemyTurnIntroIfShown(introWasRaised);
        if (restoreSpriteSorting)
            RestoreAllEnemyTurnSpriteSortingOrders();
    }

    private EnemyController GetFirstActingEnemyForTurn()
    {
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            var enemy = _activeEnemies[i];
            if (enemy != null && enemy.IsAlive && enemy.IsActiveInRoster)
                return enemy;
        }

        return null;
    }

    private void RefreshAllEnemyTurnSpriteDefaultMaterials()
    {
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            var enemy = _activeEnemies[i];
            if (enemy == null || !enemy.IsAlive || !enemy.IsActiveInRoster)
                continue;

            enemy.TurnSpriteSorting?.RefreshDefaultMaterials(enemy.CombatPresentation);
        }
    }

    private void ApplyEnemyTurnSpriteSortingForIntentPhase(EnemyController firstActingEnemy)
    {
        if (firstActingEnemy != null)
            ApplyActiveEnemyTurnSpriteSorting(firstActingEnemy);
        else
            ApplyInactiveEnemyTurnSpriteSortingForAll();
    }

    private void ApplyInactiveEnemyTurnSpriteSortingForAll()
    {
        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            var enemy = _activeEnemies[i];
            if (enemy == null || !enemy.IsAlive || !enemy.IsActiveInRoster)
                continue;

            enemy.TurnSpriteSorting?.SetInactiveTurnSorting();
        }
    }

    private void ApplyActiveEnemyTurnSpriteSorting(EnemyController actingEnemy)
    {
        if (actingEnemy == null)
            return;

        for (var i = 0; i < _activeEnemies.Count; i++)
        {
            var enemy = _activeEnemies[i];
            if (enemy == null || !enemy.IsAlive || !enemy.IsActiveInRoster)
                continue;

            if (enemy == actingEnemy)
                enemy.TurnSpriteSorting?.SetActiveTurnSorting();
            else
                enemy.TurnSpriteSorting?.SetInactiveTurnSorting();
        }
    }

    private void RestoreAllEnemyTurnSpriteSortingOrders()
    {
        for (var i = 0; i < _activeEnemies.Count; i++)
            _activeEnemies[i]?.TurnSpriteSorting?.RestoreDefaultSorting();
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

        if (enemyTurnIntentSequence != null)
            yield return enemyTurnIntentSequence.CoWaitBeforeCloseTrigger();

        if (enemyTurnIntentSequence != null && enemyTurnIntentSequence.UsesTurnIndicatorAnimator)
        {
            yield return enemyTurnIntentSequence.CoPresentTurnIndicatorOutro();
            enemyTurnIntroCanvasGroup.alpha = 0f;
            enemyTurnIntroRoot.SetActive(false);
            yield break;
        }

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
        // Remove any enemies that just died (multi-enemy); victory only once they are ALL down.
        PruneDefeatedEnemies();
        if (AnyEnemyAlive()) return false;
        // Enemy still at 0 HP on later ticks (turn start, thorns, etc.) must not re-fire victory UI.
        if (currentState == CombatState.Victory)
            return true;

        ChangeState(CombatState.Victory);

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
        if (_activeEnemies.Count == 0) return;
        var snapshot = new List<EnemyController>(_activeEnemies);
        foreach (var enemy in snapshot)
        {
            if (enemy == null) continue;
            var hp = enemy.GetCurrentHealth();
            if (hp > 0)
                enemy.TakeTrueDamage(hp);
        }

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
        ClearAllEnemyPools();
        channeledFaces.Clear();
        _pendingAfterPhysicalApplyStatuses.Clear();
        _afterPhysicalDeferredStatusPhaseCompleted = false;
        _pendingRerollGrants = 0;
        _rollBatchPipelineRunning = false;
        _pendingTopFaceByDieIndex = null;
        _pendingDieSourceByIndex = null;
        ClearPostSubmitTriggeringRerollState();
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
        _gemScheduledBatchRerolls.Clear();
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