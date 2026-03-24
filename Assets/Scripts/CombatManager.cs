using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatManager : MonoBehaviour
{
    [Header("Data & Physics")]
    public PlayerDataSO playerData;
    public DiceSpawner spawner;

    [Header("Participants")]
    public EnemyController activeEnemy;
    // Note: You can add public PlayerStatus player; here once that script is created

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
        if (playerData == null || spawner == null)
            UnityEngine.Debug.LogError("CombatManager: Essential references (PlayerData or Spawner) are missing!");
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
    }

    private void OnDisable()
    {
        CombatEvents.OnDieToggled -= HandleDieToggle;
        CombatEvents.OnRollCommand -= ExecuteBatchRoll;
        CombatEvents.OnBustResolved -= ResolveBust;
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
        // Formula: X = 12 + (6 for every die owned beyond the first two)
        int extraDice = Mathf.Max(0, playerData.currentDeck.Count - 2);
        maxPower = baseMaxPower + (extraDice * 6);
        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
    }

    private void HandleDieToggle(DieAssetSO die)
    {
        if (currentState != CombatState.WaitingForRoll) return;

        if (selectedDice.Contains(die))
            selectedDice.Remove(die);
        else
            selectedDice.Add(die);
    }

    private void ExecuteBatchRoll()
    {
        if (currentState != CombatState.WaitingForRoll || selectedDice.Count == 0) return;

        expectedDiceCount = selectedDice.Count;
        diceSettledCount = 0;

        ChangeState(CombatState.Rolling);

        // Launch the physical dice
        spawner.SpawnAndRollBatch(selectedDice);

        // Clear selection for the next window
        selectedDice.Clear();
    }

    /// <summary>
    /// Triggered by individual DiceRoller scripts when they stop moving.
    /// </summary>
    public void ResolveRollResult(DieFaceSO face)
    {
        if (face.type == DieType.Attack) pendingAttack += face.value;
        else pendingDefense += face.value;

        currentPower += face.value;
        ApplyFaceEffect(face.effect, face.value);

        // Update UI immediately for each die
        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
        CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);

        diceSettledCount++;

        // Only move to logic check when the WHOLE batch is still
        if (diceSettledCount >= expectedDiceCount)
        {
            CheckBustStatus();
        }
    }

    private void CheckBustStatus()
    {
        // PERFECT HIT: Double the pools
        if (currentPower == maxPower)
        {
            UnityEngine.Debug.Log("<color=cyan>PERFECT HIT! Doubling Pending Values.</color>");
            pendingAttack *= 2;
            pendingDefense *= 2;
            CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);

            SubmitTurn();
        }
        // BUST: Trigger the UI choice
        else if (currentPower > maxPower)
        {
            ChangeState(CombatState.BustCheck);
            CombatEvents.OnBustOccurred?.Invoke();
        }
        // UNDER: Let the player roll more
        else
        {
            ChangeState(CombatState.WaitingForRoll);
        }
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

        // 1. Apply Player's Offense to Enemy
        if (pendingAttack > 0 && activeEnemy != null)
        {
            activeEnemy.TakeDamage(pendingAttack);
        }

        // 2. Start the automated Enemy sequence
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(1.2f); // "Thinking" pause

        if (activeEnemy != null)
        {
            EnemyActionSO action = activeEnemy.GetCurrentAction();
            UnityEngine.Debug.Log($"Enemy uses: {action.actionName}");

            if (action.damage > 0)
            {
                // TODO: player.TakeDamage(action.damage);
                UnityEngine.Debug.Log($"<color=red>Enemy deals {action.damage} damage!</color>");
            }

            // Move to next move for the next round
            activeEnemy.PrepareNextAction();
        }

        yield return new WaitForSeconds(1.0f); // Recovery pause
        ResetTurn();
    }

    private void ResetTurn()
    {
        pendingAttack = 0;
        pendingDefense = 0;
        currentPower = 0;

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