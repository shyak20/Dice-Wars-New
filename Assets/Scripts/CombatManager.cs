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
    public PlayerDataSO playerData;
    public DiceSpawner spawner;

    [Header("Balancing")]
    public int baseMaxPower = 12;

    [SerializeField] private int StartStrikeMultiplier = 2;
    

    [Header("Testing")]
    [SerializeField] private TestStartingFacesSO testStartingFaces;

    private CombatState currentState;
    private List<DieAssetSO> selectedDice = new List<DieAssetSO>();

    private int currentPower;
    private int maxPower;
    private int bonusAttack;
    private int bonusDefense;
    private int overchargeBonus;
    private int appliedMultiplier;
    private bool bustProtected;

    private List<FaceResult> channeledFaces = new List<FaceResult>();
    private List<Action<GameActionContext>> turnEndActions = new List<Action<GameActionContext>>();

    private int diceSettledCount = 0;
    private int expectedDiceCount = 0;

    public int GetPendingAttack() =>
        channeledFaces.Where(f => f.Type == DieType.Attack).Sum(f => f.Value) + bonusAttack;

    public int GetPendingDefense() =>
        channeledFaces.Where(f => f.Type == DieType.Defense).Sum(f => f.Value) + bonusDefense;

    public List<FaceResult> GetChanneledFaces() => channeledFaces;

    public void AddBonusAttack(int amount) => bonusAttack += amount;
    public void AddBonusDefense(int amount) => bonusDefense += amount;
    public void AddOvercharge(int amount) => overchargeBonus += amount;
    public int GetAppliedMultiplier() => appliedMultiplier;
    public void SetBustProtected() => bustProtected = true;
    public void RefundPower(int amount) => currentPower -= amount;
    public void QueueTurnEndAction(Action<GameActionContext> action) => turnEndActions.Add(action);

    private void Awake()
    {
        if (playerData == null || spawner == null || player == null || activeEnemy == null)
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
        ApplyTestStartingFaces();
        selectedDice.Clear();
        channeledFaces.Clear();
        bonusAttack = 0;
        bonusDefense = 0;
        overchargeBonus = 0;
        appliedMultiplier = StartStrikeMultiplier;
        bustProtected = false;
        currentPower = 0;
        CalculateMaxPower();
        CombatEvents.OnPoolsUpdated?.Invoke(0, 0);
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

        var runtimeDeck = Instantiate(playerData);
    
        for (int d = 0; d < runtimeDeck.currentDeck.Count; d++)
        {
            var clonedDie = Instantiate(runtimeDeck.currentDeck[d]);
            clonedDie.name = runtimeDeck.currentDeck[d].name + " (Test)";

            if (testStartingFaces.perCube)
            {
                var face = validFaces[d % validFaces.Count];
                for (int i = 0; i < clonedDie.faces.Length; i++)
                {
                    clonedDie.faces[i] = face;
                }
            }
            else if (testStartingFaces.changeAll)
            {
                for (int i = 0; i < clonedDie.faces.Length; i++)
                    clonedDie.faces[i] = validFaces[i % validFaces.Count];
            }
            else
            {
                for (int i = 0; i < validFaces.Count && i < clonedDie.faces.Length; i++)
                    clonedDie.faces[i] = validFaces[i];
            }

            runtimeDeck.currentDeck[d] = clonedDie;
        }

        playerData = runtimeDeck;
        Debug.Log($"[TestStartingFaces] Applied {validFaces.Count} test face(s) to {runtimeDeck.currentDeck.Count} dice (ChangeAll: {testStartingFaces.changeAll})");
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

        ChangeState(CombatState.Rolling);
        spawner.SpawnAndRollBatch(selectedDice);
    }

    public void ResolveRollResult(DieFaceSO face)
    {
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
        if (diceSettledCount >= expectedDiceCount) CheckBustStatus();
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

    private void CheckBustStatus()
    {
        int pendingAttack = GetPendingAttack();
        int pendingDefense = GetPendingDefense();

        if (currentPower == maxPower)
        {
            appliedMultiplier += overchargeBonus;

            foreach (var face in channeledFaces)
                face.Value *= appliedMultiplier;
            bonusAttack *= appliedMultiplier;
            bonusDefense *= appliedMultiplier;

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
            bonusAttack = 0;
        }
        else
        {
            channeledFaces.RemoveAll(f => f.Type == DieType.Defense);
            bonusDefense = 0;
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

        int pendingAttack = GetPendingAttack();
        int pendingDefense = GetPendingDefense();

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
            EnemyActionSO action = activeEnemy.GetCurrentAction();
            if (action.damage > 0)
            {
                for (int i = 0; i < action.numberOfAttacks; i++)
                {
                    player.TakeDamage(action.damage);

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
        channeledFaces.Clear();
        turnEndActions.Clear();
        bonusAttack = 0;
        bonusDefense = 0;
        overchargeBonus = 0;
        appliedMultiplier = StartStrikeMultiplier;
        bustProtected = false;
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
