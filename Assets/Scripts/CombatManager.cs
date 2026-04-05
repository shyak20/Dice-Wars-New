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

    [Header("Balancing")]
    public int baseMaxPower = 12;
    [SerializeField] private int StartStrikeMultiplier = 2;

    [Header("UI Panels")]
    [SerializeField] private PrecisionPanel precisionPanel;
    [Tooltip("Optional. When assigned, perfect strike waits for jackpot UI (overlay + ×multiplier on pools) before SubmitTurn.")]
    [SerializeField] private JackpotPresentationController jackpotPresentation;

    [Header("Status Effect UI")]
    [SerializeField] private StatusEffectBarUI playerStatusBar;
    [SerializeField] private StatusEffectBarUI enemyStatusBar;

    [Header("Testing")]
    [SerializeField] private TestStartingFacesSO testStartingFaces;

    private CombatState currentState;
    private List<DieAssetSO> selectedDice = new List<DieAssetSO>();

    private int currentPower;
    private int maxPower;
    private int overchargeBonus;
    private int appliedMultiplier;
    private bool bustProtected;
    private bool immune;
    private int thorns;
    private bool kineticShieldActive;
    private int kineticShieldBonus;

    private List<FaceResult> channeledFaces = new List<FaceResult>();
    private List<Action<GameActionContext>> turnEndActions = new List<Action<GameActionContext>>();
    private Queue<int> pendingPrecisionChoices = new Queue<int>();

    private int diceSettledCount = 0;
    private int expectedDiceCount = 0;
    private int pendingRollVisualSequences;

    private int rollsRemaining;
    private int maxRolls;
    private bool _appliedTestStartingFaces;

    // Updated summation logic to pull from FaceResult properties
    public int GetPendingAttack() => channeledFaces.Sum(f => f.Damage);
    public int GetPendingDefense() => channeledFaces.Sum(f => f.Armor) + kineticShieldBonus;

    public List<FaceResult> GetChanneledFaces() => channeledFaces;

    private Dictionary<DieType, int> BuildElementPools()
    {
        var pools = new Dictionary<DieType, int>();
        foreach (DieType type in Enum.GetValues(typeof(DieType)))
            pools[type] = 0;

        // Sum values across all faces in the container
        foreach (var face in channeledFaces)
        {
            pools[DieType.Damage] += face.Damage;
            pools[DieType.Armor] += face.Armor;

            // Only add extra elements if they aren't Damage/Armor to avoid double-counting
            if (face.Type != DieType.Damage && face.Type != DieType.Armor)
                pools[face.Type] += face.Value;
        }

        pools[DieType.Armor] += kineticShieldBonus;
        return pools;
    }

    private Dictionary<DieType, int> SnapshotPools()
    {
        var src = BuildElementPools();
        var copy = new Dictionary<DieType, int>();
        foreach (var kvp in src)
            copy[kvp.Key] = kvp.Value;
        return copy;
    }

    private void FirePoolsUpdated() => CombatEvents.OnPoolsUpdated?.Invoke(BuildElementPools());

    private void NotifyAllPoolUI()
    {
        var pools = BuildElementPools();
        CombatEvents.OnPoolsUpdated?.Invoke(pools);
        CombatEvents.OnPoolIconsFullResync?.Invoke(pools);
    }

    public void AddOvercharge(int amount) => overchargeBonus += amount;
    public int GetAppliedMultiplier() => appliedMultiplier;
    public void SetBustProtected() => bustProtected = true;
    public void SetImmune() => immune = true;
    public void AddThorns(int amount) => thorns += amount;
    public void ActivateKineticShield() => kineticShieldActive = true;
    public void RefundPower(int amount) => currentPower -= amount;
    public void QueuePrecisionChoice(int amount) => pendingPrecisionChoices.Enqueue(amount);
    public void QueueTurnEndAction(Action<GameActionContext> action) => turnEndActions.Add(action);

    private PlayerDataSO playerData;

    private void Awake()
    {
        if (spawner == null || player == null || activeEnemy == null)
            Debug.LogError("CombatManager: Missing references!");
    }

    private void Start()
    {
        InitializeEnemy();
        InitializeCombat();
    }

    private void InitializeEnemy()
    {
        if (RunManager.Instance != null)
        {
            var room = RunManager.Instance.CurrentRoom;
            if (room == null || room.roomType != RoomType.Combat) return;
            activeEnemy.Initialize(room.enemyType);
            return;
        }
        if (activeEnemy.enemyData != null) activeEnemy.Initialize(activeEnemy.enemyData);
    }

    private void OnEnable()
    {
        CombatEvents.OnDieToggled += HandleDieToggle;
        CombatEvents.OnRollCommand += ExecuteBatchRoll;
        CombatEvents.OnBustResolved += ResolveBust;
        CombatEvents.OnEndTurnPressed += ManualEndTurn;
    }

    private void OnDisable()
    {
        CombatEvents.OnDieToggled -= HandleDieToggle;
        CombatEvents.OnRollCommand -= ExecuteBatchRoll;
        CombatEvents.OnBustResolved -= ResolveBust;
        CombatEvents.OnEndTurnPressed -= ManualEndTurn;
    }

    private void InitializeCombat()
    {
        if (PlayerDataContainer.Instance == null) return;
        playerData = PlayerDataContainer.Instance.RuntimeData;
        ApplyTestStartingFaces();
        ResetStats();
        ChangeState(CombatState.WaitingForRoll);
    }

    private void ResetStats()
    {
        selectedDice.Clear();
        channeledFaces.Clear();
        overchargeBonus = 0;
        appliedMultiplier = StartStrikeMultiplier;
        bustProtected = false;
        immune = false;
        thorns = 0;
        kineticShieldActive = false;
        kineticShieldBonus = 0;
        pendingPrecisionChoices.Clear();
        currentPower = 0;
        maxRolls = playerData.maxRollsPerTurn;
        rollsRemaining = maxRolls;
        CalculateMaxPower();
        NotifyAllPoolUI();
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);

        if (playerStatusBar != null) playerStatusBar.Bind(player.StatusEffects);
        if (enemyStatusBar != null) enemyStatusBar.Bind(activeEnemy.StatusEffects);
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

    private void CalculateMaxPower()
    {
        int extraDice = Mathf.Max(0, playerData.currentDeck.Count - 2);
        maxPower = baseMaxPower + (extraDice * 6);
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
        expectedDiceCount = selectedDice.Count;
        diceSettledCount = 0;
        pendingRollVisualSequences = 0;
        rollsRemaining--;
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);
        ChangeState(CombatState.Rolling);
        spawner.SpawnAndRollBatch(selectedDice);
    }

    public void ResolveRollResult(DieFaceSO face, Transform dieWorldSource = null)
    {
        bool kineticArmorThisRoll = kineticShieldActive;
        if (kineticArmorThisRoll) kineticShieldBonus++;

        var statusCtx = BuildStatusContext();
        var modifiedValue = player.StatusEffects.ModifyFaceValue(statusCtx, face.value);
        var rolledDamage = face.damage;
        if (rolledDamage > 0)
            rolledDamage += player.StatusEffects.GetTotalPerDieAttackDamageBonus(statusCtx);

        // Populate the expanded FaceResult
        var result = new FaceResult
        {
            Face = face,
            Value = modifiedValue,
            Type = face.type,
            Damage = rolledDamage,
            Armor = face.armor,
            ActivateImmediately = face.activateImmediately,
            Action = face.action
        };

        channeledFaces.Add(result);
        currentPower += modifiedValue;

        // Immediate Action Execution logic
        if (result.Action != null && result.ActivateImmediately)
        {
            var context = BuildContext(result);
            result.Action.Execute(context);
        }

        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
        FirePoolsUpdated();

        diceSettledCount++;

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

        TryAdvanceAfterDiceSettledAndRollVisuals();
    }

    private void OnRollVisualSequenceFinished()
    {
        pendingRollVisualSequences--;
        if (pendingRollVisualSequences < 0)
        {
            Debug.LogError("CombatManager: pendingRollVisualSequences underflow — check DiceRollVisualPayload.ReportVisualFinished is called once per payload.");
            pendingRollVisualSequences = 0;
        }

        TryAdvanceAfterDiceSettledAndRollVisuals();
    }

    private void TryAdvanceAfterDiceSettledAndRollVisuals()
    {
        if (diceSettledCount < expectedDiceCount) return;
        if (pendingRollVisualSequences > 0) return;
        ProcessPrecisionQueue();
    }

    private static List<RollOutcomeVisualLine> BuildRollVisualLines(FaceResult result, bool kineticArmorThisRoll)
    {
        var lines = new List<RollOutcomeVisualLine>();
        void AddLine(DieType t, int amt)
        {
            if (amt <= 0) return;
            lines.Add(new RollOutcomeVisualLine { Type = t, Amount = amt });
        }

        AddLine(DieType.Damage, result.Damage);
        AddLine(DieType.Armor, result.Armor);
        if (result.Type != DieType.Damage && result.Type != DieType.Armor)
            AddLine(result.Type, result.Value);
        if (kineticArmorThisRoll)
            AddLine(DieType.Armor, 1);
        return lines;
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

    private StatusEffectContext BuildStatusContext() => new StatusEffectContext { CombatManager = this, Player = player, Enemy = activeEnemy };

    private void ProcessPrecisionQueue()
    {
        if (pendingPrecisionChoices.Count > 0)
        {
            var amount = pendingPrecisionChoices.Dequeue();
            precisionPanel.Show(amount, accepted => {
                if (accepted) { currentPower += amount; CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower); }
                ProcessPrecisionQueue();
            });
        }
        else CheckBustStatus();
    }

    private void CheckBustStatus()
    {
        if (currentPower == maxPower)
        {
            var poolsBefore = SnapshotPools();
            appliedMultiplier += overchargeBonus;
            int jackpotMultiplier = appliedMultiplier;
            foreach (var face in channeledFaces)
            {
                face.Damage *= appliedMultiplier;
                face.Armor *= appliedMultiplier;
            }

            kineticShieldBonus *= appliedMultiplier;
            activeEnemy.StatusEffects.TickPerfectStrike(BuildStatusContext());
            var poolsAfter = SnapshotPools();
            if (CheckVictory())
            {
                NotifyAllPoolUI();
                return;
            }

            if (jackpotPresentation != null)
                StartCoroutine(CoFinishJackpotAfterPresentation(jackpotMultiplier, poolsBefore, poolsAfter));
            else
            {
                NotifyAllPoolUI();
                SubmitTurn();
            }
        }
        else if (currentPower > maxPower)
        {
            if (bustProtected) SubmitTurn();
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

    private void ResolveBust(bool nullifyDamage)
    {
        foreach (var face in channeledFaces)
        {
            if (nullifyDamage) face.Damage = 0;
            else { face.Armor = 0; kineticShieldBonus = 0; }
        }
        NotifyAllPoolUI();
        SubmitTurn();
    }

    private void SubmitTurn()
    {
        ChangeState(CombatState.TurnEnd);
        var ctx = BuildContext();

        // Execution of stored/deferred actions
        foreach (var face in channeledFaces)
        {
            if (face.Action != null && !face.ActivateImmediately)
                face.Action.Execute(ctx);
        }

        foreach (var action in turnEndActions) action.Invoke(ctx);
        turnEndActions.Clear();

        var statusCtx = BuildStatusContext();
        int pendingAttack = GetPendingAttack();
        pendingAttack += player.StatusEffects.GetTotalBonusAttack(statusCtx);
        pendingAttack = activeEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, pendingAttack);
        int pendingDefense = GetPendingDefense();

        if (pendingAttack > 0)
        {
            activeEnemy.TakeDamage(pendingAttack);
            if (CheckVictory()) return;
        }
        if (pendingDefense > 0) player.AddArmor(pendingDefense);

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator CoFinishJackpotAfterPresentation(int multiplier, Dictionary<DieType, int> poolsBefore, Dictionary<DieType, int> poolsAfter)
    {
        // Unity does not reliably run a nested IEnumerator with "yield return routine()"; must use StartCoroutine.
        yield return StartCoroutine(jackpotPresentation.Run(multiplier, poolsBefore, poolsAfter));
        NotifyAllPoolUI();
        SubmitTurn();
    }

    private IEnumerator EnemyTurnRoutine()
    {
        ChangeState(CombatState.EnemyTurn);
        yield return new WaitForSeconds(1.0f);
        if (activeEnemy != null && player != null)
        {
            activeEnemy.ResetArmor();
            var statusCtx = BuildStatusContext();
            activeEnemy.StatusEffects.TickBeforeEnemyTurn(statusCtx);
            if (CheckVictory()) yield break;

            EnemyActionSO action = activeEnemy.GetCurrentAction();
            if (action.damage > 0)
            {
                for (int i = 0; i < action.numberOfAttacks; i++)
                {
                    var damage = activeEnemy.StatusEffects.ModifyEnemyHitDamage(statusCtx, action.damage);
                    if (activeEnemy.StatusEffects.CheckRedirectAttackToSelf(statusCtx)) activeEnemy.TakeDamage(damage);
                    else { if (immune) damage = Mathf.Min(damage, 1); player.TakeDamage(damage); if (thorns > 0) activeEnemy.TakeDamage(thorns); }
                    if (CheckDefeat()) yield break;
                    if (action.numberOfAttacks > 1) yield return new WaitForSeconds(0.4f);
                }
            }
            if (action.armor > 0) activeEnemy.AddArmor(action.armor);
            activeEnemy.StatusEffects.TickAfterEnemyTurn(statusCtx);
            if (CheckVictory()) yield break;
            activeEnemy.PrepareNextAction();
        }
        yield return new WaitForSeconds(1.0f);
        ResetTurn();
    }

    private bool CheckVictory() { if (activeEnemy.GetCurrentHealth() <= 0) { ChangeState(CombatState.Victory); CombatEvents.OnPlayerVictory?.Invoke(); return true; } return false; }
    private bool CheckDefeat() { if (player.GetCurrentHealth() <= 0) { ChangeState(CombatState.Defeat); CombatEvents.OnPlayerDefeat?.Invoke(); return true; } return false; }

    private void ResetTurn()
    {
        channeledFaces.Clear();
        turnEndActions.Clear();
        overchargeBonus = 0;
        appliedMultiplier = StartStrikeMultiplier;
        bustProtected = false; immune = false; thorns = 0;
        kineticShieldActive = false; kineticShieldBonus = 0;
        pendingPrecisionChoices.Clear();
        currentPower = 0; rollsRemaining = maxRolls;
        player.ResetArmor();
        var statusCtx = BuildStatusContext();
        activeEnemy.StatusEffects.TickTurnStart(statusCtx);
        player.StatusEffects.TickTurnStart(statusCtx);
        NotifyAllPoolUI();
        CombatEvents.OnPowerChanged?.Invoke(0, maxPower);
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);
        ChangeState(CombatState.WaitingForRoll);
    }

    private void ChangeState(CombatState newState) { currentState = newState; CombatEvents.OnStateChanged?.Invoke(newState); }
}