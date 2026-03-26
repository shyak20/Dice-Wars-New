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

    private CombatState currentState;
    private List<DieAssetSO> selectedDice = new List<DieAssetSO>();

    private int currentPower;
    private int maxPower;
    private int pendingAttack;
    private int pendingDefense;

    private int diceSettledCount = 0;
    private int expectedDiceCount = 0;

    private void Awake()
    {
        if (playerData == null || spawner == null || player == null || activeEnemy == null)
            UnityEngine.Debug.LogError("CombatManager: Missing references!");
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

        // Note: selectedDice.Clear() remains removed to keep your selection persistent.
    }

    public void ResolveRollResult(DieFaceSO face)
    {
        if (face.type == DieType.Attack) pendingAttack += face.value;
        else pendingDefense += face.value;

        currentPower += face.value;
        CombatEvents.OnPowerChanged?.Invoke(currentPower, maxPower);
        CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);

        diceSettledCount++;
        if (diceSettledCount >= expectedDiceCount) CheckBustStatus();
    }

    private void CheckBustStatus()
    {
        if (currentPower == maxPower)
        {
            pendingAttack *= 2;
            pendingDefense *= 2;
            CombatEvents.OnPoolsUpdated?.Invoke(pendingAttack, pendingDefense);
            SubmitTurn();
        }
        else if (currentPower > maxPower)
        {
            ChangeState(CombatState.BustCheck);
            CombatEvents.OnBustOccurred?.Invoke(pendingAttack, pendingDefense);
        }
        else
        {
            ChangeState(CombatState.WaitingForRoll);
        }
    }

    private void ManualEndTurn()
    {
        if (currentState == CombatState.WaitingForRoll) SubmitTurn();
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

        // Player deals damage
        if (pendingAttack > 0)
        {
            activeEnemy.TakeDamage(pendingAttack);
            // Check if enemy died after the player hit them
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
            EnemyActionSO action = activeEnemy.GetCurrentAction();
            if (action.damage > 0)
            {
                for (int i = 0; i < action.numberOfAttacks; i++)
                {
                    player.TakeDamage(action.damage);

                    // Check if player died during the multi-attack
                    if (CheckDefeat()) yield break;

                    if (action.numberOfAttacks > 1) yield return new WaitForSeconds(0.4f);
                }
            }
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
        pendingAttack = 0;
        pendingDefense = 0;
        currentPower = 0;
        player.ResetArmor();
        CombatEvents.OnPoolsUpdated?.Invoke(0, 0);
        CombatEvents.OnPowerChanged?.Invoke(0, maxPower);
        ChangeState(CombatState.WaitingForRoll);
    }

    private void ChangeState(CombatState newState)
    {
        currentState = newState;
        CombatEvents.OnStateChanged?.Invoke(newState);
    }
}