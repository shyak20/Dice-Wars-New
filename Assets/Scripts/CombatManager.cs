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

    private int rollsRemaining;
    private int maxRolls;

    public int GetPendingAttack() =>
        channeledFaces.Where(f => f.Type == DieType.Attack).Sum(f => f.Value);

    public int GetPendingDefense() =>
        channeledFaces.Where(f => f.Type == DieType.Defense).Sum(f => f.Value) + kineticShieldBonus;

    public List<FaceResult> GetChanneledFaces() => channeledFaces;

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

    private void Start() => InitializeCombat();

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
        if (PlayerDataContainer.Instance == null)
        {
            Debug.LogError("CombatManager: PlayerDataContainer not found in scene!");
            return;
        }

        playerData = PlayerDataContainer.Instance.RuntimeData;
        ApplyTestStartingFaces();
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
        CombatEvents.OnPoolsUpdated?.Invoke(0, 0);
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);
        ChangeState(CombatState.WaitingForRoll);
    }

    private void ApplyTestStartingFaces()
    {
#if UNITY_EDITOR
        if (testStartingFaces == null || !testStartingFaces.isActive)
            return;

        var validFaces = testStartingFaces.testFaces.FindAll(f => f != null);
        if (validFaces.Count == 0)
        {
            Debug.LogWarning("TestStartingFaces is active but has no valid faces assigned.");
            return;
        }

        for (var d = 0; d < playerData.currentDeck.Count; d++)
        {
            var die = playerData.currentDeck[d];

            if (testStartingFaces.perCube)
            {
                var face = validFaces[d % validFaces.Count];
                for (var i = 0; i < die.faces.Length; i++)
                    die.faces[i] = face;
            }
            else if (testStartingFaces.changeAll)
            {
                for (var i = 0; i < die.faces.Length; i++)
                    die.faces[i] = validFaces[i % validFaces.Count];
            }
            else
            {
                for (var i = 0; i < validFaces.Count && i < die.faces.Length; i++)
                    die.faces[i] = validFaces[i];
            }
        }

        Debug.Log($"<color=red>[TestStartingFaces] Applied {validFaces.Count} test face(s) </color> to {playerData.currentDeck.Count} dice (ChangeAll: {testStartingFaces.changeAll})");
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

        rollsRemaining--;
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);

        ChangeState(CombatState.Rolling);
        spawner.SpawnAndRollBatch(selectedDice);
    }

    public void ResolveRollResult(DieFaceSO face)
    {
        if (kineticShieldActive)
        {
            kineticShieldBonus++;
            if (GameActionDebug.Enabled)
                Debug.Log($"[KineticShield] +1 bonus armor (total: {kineticShieldBonus})");
        }

        var result = new FaceResult
        {
            Face = face,
            Value = face.value,
            Type = face.type,
            Action = face.action
        };

        channeledFaces.Add(result);
        currentPower += face.value;

        if (result.Action != null)
        {
            var context = BuildContext(result);
            result.Action.Execute(context);
        }

        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
        CombatEvents.OnPoolsUpdated?.Invoke(GetPendingAttack(), GetPendingDefense());

        diceSettledCount++;
        if (diceSettledCount >= expectedDiceCount) ProcessPrecisionQueue();
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

    private StatusEffectContext BuildStatusContext()
    {
        return new StatusEffectContext
        {
            CombatManager = this,
            Player = player,
            Enemy = activeEnemy
        };
    }

    private void ProcessPrecisionQueue()
    {
        if (pendingPrecisionChoices.Count > 0)
        {
            var amount = pendingPrecisionChoices.Dequeue();
            precisionPanel.Show(amount, accepted =>
            {
                if (accepted)
                {
                    currentPower += amount;
                    CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
                }
                ProcessPrecisionQueue();
            });
        }
        else
        {
            CheckBustStatus();
        }
    }

    private void CheckBustStatus()
    {
        int pendingAttack = GetPendingAttack();
        int pendingDefense = GetPendingDefense();

        if (currentPower == maxPower)
        {
            appliedMultiplier += overchargeBonus;

            foreach (var face in channeledFaces)
                face.Value *= appliedMultiplier;
            kineticShieldBonus *= appliedMultiplier;

            activeEnemy.StatusEffects.TickPerfectStrike(BuildStatusContext());
            if (CheckVictory()) return;

            CombatEvents.OnPoolsUpdated?.Invoke(GetPendingAttack(), GetPendingDefense());
            SubmitTurn();
        }
        else if (currentPower > maxPower)
        {
            if (bustProtected)
            {
                SubmitTurn();
            }
            else
            {
                ChangeState(CombatState.BustCheck);
                CombatEvents.OnBustOccurred?.Invoke(pendingAttack, pendingDefense);
            }
        }
        else
        {
            if (rollsRemaining <= 0)
                SubmitTurn();
            else
                ChangeState(CombatState.WaitingForRoll);
        }
    }

    private void ManualEndTurn()
    {
        if (currentState == CombatState.WaitingForRoll) SubmitTurn();
    }

    private void ResolveBust(bool nullifyAttack)
    {
        if (nullifyAttack)
        {
            channeledFaces.RemoveAll(f => f.Type == DieType.Attack);
        }
        else
        {
            channeledFaces.RemoveAll(f => f.Type == DieType.Defense);
            kineticShieldBonus = 0;
        }

        CombatEvents.OnPoolsUpdated?.Invoke(GetPendingAttack(), GetPendingDefense());
        SubmitTurn();
    }

    private void SubmitTurn()
    {
        ChangeState(CombatState.TurnEnd);

        var turnEndContext = BuildContext();
        foreach (var action in turnEndActions)
            action.Invoke(turnEndContext);
        turnEndActions.Clear();

        var statusCtx = BuildStatusContext();

        int pendingAttack = GetPendingAttack();
        pendingAttack += player.StatusEffects.GetTotalBonusAttack(statusCtx);
        pendingAttack = activeEnemy.StatusEffects.ApplyDamageModifiers(statusCtx, pendingAttack);
        int pendingDefense = GetPendingDefense();

        CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);

        if (pendingAttack > 0)
        {
            activeEnemy.TakeDamage(pendingAttack);
            if (CheckVictory()) return;
        }

        if (pendingDefense > 0) player.AddArmor(pendingDefense);

        StartCoroutine(EnemyTurnRoutine());
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
                    damage = Mathf.Max(0, damage);

                    if (activeEnemy.StatusEffects.CheckRedirectAttackToSelf(statusCtx))
                    {
                        activeEnemy.TakeDamage(damage);
                        if (CheckVictory()) yield break;
                    }
                    else
                    {
                        if (immune) damage = Mathf.Min(damage, 1);
                        player.TakeDamage(damage);

                        if (thorns > 0)
                        {
                            activeEnemy.TakeDamage(thorns);
                            if (CheckVictory()) yield break;
                        }

                        if (CheckDefeat()) yield break;
                    }

                    if (action.numberOfAttacks > 1) yield return new WaitForSeconds(0.4f);
                }
            }

            if (action.armor > 0)
                activeEnemy.AddArmor(action.armor);

            activeEnemy.StatusEffects.TickAfterEnemyTurn(statusCtx);
            if (CheckVictory()) yield break;

            activeEnemy.PrepareNextAction();
        }

        yield return new WaitForSeconds(1.0f);
        ResetTurn();
    }

    private bool CheckVictory()
    {
        if (activeEnemy.GetCurrentHealth() <= 0)
        {
            ChangeState(CombatState.Victory);
            CombatEvents.OnPlayerVictory?.Invoke();
            return true;
        }
        return false;
    }

    private bool CheckDefeat()
    {
        if (player.GetCurrentHealth() <= 0)
        {
            ChangeState(CombatState.Defeat);
            CombatEvents.OnPlayerDefeat?.Invoke();
            return true;
        }
        return false;
    }

    private void ResetTurn()
    {
        channeledFaces.Clear();
        turnEndActions.Clear();
        overchargeBonus = 0;
        appliedMultiplier = StartStrikeMultiplier;
        bustProtected = false;
        immune = false;
        thorns = 0;
        kineticShieldActive = false;
        kineticShieldBonus = 0;
        pendingPrecisionChoices.Clear();
        currentPower = 0;
        rollsRemaining = maxRolls;
        player.ResetArmor();

        var statusCtx = BuildStatusContext();
        activeEnemy.StatusEffects.TickTurnStart(statusCtx);
        player.StatusEffects.TickTurnStart(statusCtx);

        CombatEvents.OnPoolsUpdated?.Invoke(0, 0);
        CombatEvents.OnPowerChanged?.Invoke(0, maxPower);
        CombatEvents.OnRollsRemainingChanged?.Invoke(rollsRemaining, maxRolls);
        ChangeState(CombatState.WaitingForRoll);
    }

    private void ChangeState(CombatState newState)
    {
        currentState = newState;
        CombatEvents.OnStateChanged?.Invoke(newState);
    }
}
