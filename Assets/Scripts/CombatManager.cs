using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatManager : MonoBehaviour
{
    [Header("Participants")]
    public PlayerStatus player;
    public EnemyController activeEnemy;

    [Header("Data & Physics")]
    public PlayerDataSO playerData;
    public DiceSpawner spawner;

    [Header("Balancing")]
    public int baseMaxPower = 12;

    // Internal State
    private CombatState currentState;
    private List<DieAssetSO> selectedDice = new List<DieAssetSO>();

    // Combat Pools
    private int currentPower;
    private int maxPower;
    private int pendingAttack;
    private int pendingDefense;

    // Batch Processing
    private int diceSettledCount = 0;
    private int expectedDiceCount = 0;

    private void Awake()
    {
        if (playerData == null || spawner == null || player == null || activeEnemy == null)
            UnityEngine.Debug.LogError("CombatManager: Missing essential references in the Inspector!");
    }

    private void Start()
    {
        InitializeCombat();
    }

    private void OnEnable()
    {
        CombatEvents.OnDieToggled += HandleDieToggle;
        CombatEvents.OnRollCommand += ExecuteBatchRoll;
        CombatEvents.OnBustResolved += ResolveBust;

        // New Event: Manual End Turn
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
        selectedDice.Clear();
        pendingAttack = 0;
        pendingDefense = 0;
        currentPower = 0;

        CalculateMaxPower();

        CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);
        ChangeState(CombatState.WaitingForRoll);
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

        ChangeState(CombatState.Rolling);
        spawner.SpawnAndRollBatch(selectedDice);
        selectedDice.Clear();
    }

    /// <summary>
    /// Called by individual DiceRoller scripts.
    /// </summary>
    public void ResolveRollResult(DieFaceSO face)
    {
        if (face.type == DieType.Attack) pendingAttack += face.value;
        else pendingDefense += face.value;

        currentPower += face.value;
        ApplyFaceEffect(face.effect, face.value);

        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
        CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);

        diceSettledCount++;

        if (diceSettledCount >= expectedDiceCount)
        {
            CheckBustStatus();
        }
    }

    private void CheckBustStatus()
    {
        // Condition: Reached X (Perfect Hit)
        if (currentPower == maxPower)
        {
            UnityEngine.Debug.Log("<color=cyan>PERFECT HIT! Doubling Values.</color>");
            pendingAttack *= 2;
            pendingDefense *= 2;
            CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);

            SubmitTurn();
        }
        // Condition: Passed X (Bust)
        else if (currentPower > maxPower)
        {
            ChangeState(CombatState.BustCheck);
            CombatEvents.OnBustOccurred?.Invoke();
        }
        // Condition: Below X (Continue)
        else
        {
            ChangeState(CombatState.WaitingForRoll);
        }
    }

    private void ManualEndTurn()
    {
        if (currentState != CombatState.WaitingForRoll) return;

        UnityEngine.Debug.Log("Player manually ended turn.");
        SubmitTurn();
    }

    private void ResolveBust(bool nullifyAttack)
    {
        if (nullifyAttack) pendingAttack = 0;
        else pendingDefense = 0;

        CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);
        SubmitTurn();
    }

    private void SubmitTurn()
    {
        ChangeState(CombatState.TurnEnd);

        // 1. Resolve Player Actions
        if (pendingAttack > 0) activeEnemy.TakeDamage(pendingAttack);
        if (pendingDefense > 0) player.AddArmor(pendingDefense);

        // 2. Transition to Enemy
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(1.2f);

        if (activeEnemy != null && player != null)
        {
            EnemyActionSO action = activeEnemy.GetCurrentAction();
            UnityEngine.Debug.Log($"Enemy Turn: {action.actionName}");

            // Enemy Attack Logic (Multi-hit)
            if (action.damage > 0)
            {
                for (int i = 0; i < action.numberOfAttacks; i++)
                {
                    player.TakeDamage(action.damage);
                    if (action.numberOfAttacks > 1) yield return new WaitForSeconds(0.4f);
                }
            }

            // Enemy Armor Logic
            if (action.armor > 0)
            {
                // Note: If you want Enemy Armor, add an AddArmor method to EnemyController
                UnityEngine.Debug.Log($"{activeEnemy.enemyData.enemyName} gains {action.armor} armor.");
            }

            activeEnemy.PrepareNextAction();
        }

        yield return new WaitForSeconds(1.0f);

        // Start Next Turn Cycle
        ResetTurn();
    }

    private void ResetTurn()
    {
        pendingAttack = 0;
        pendingDefense = 0;
        currentPower = 0;

        // Decide if Armor persists:
        player.ResetArmor();

        CombatEvents.OnPoolsUpdated?.Invoke(0, 0);
        CombatEvents.OnPowerChanged?.Invoke(0, maxPower);

        ChangeState(CombatState.WaitingForRoll);
    }

    private void ApplyFaceEffect(FaceEffect effect, int value)
    {
        if (effect == FaceEffect.None) return;
        // logic for Poison/Heal/ShieldBreak goes here
    }

    private void ChangeState(CombatState newState)
    {
        currentState = newState;
        CombatEvents.OnStateChanged?.Invoke(newState);
    }
}