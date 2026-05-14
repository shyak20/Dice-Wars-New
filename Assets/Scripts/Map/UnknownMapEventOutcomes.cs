using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime context for executing <see cref="UnknownMapEventOutcomeBase"/> after the player picks an option.</summary>
public readonly struct UnknownMapEventOutcomeContext
{
    public UnknownMapEventOutcomeContext(
        UnknownMapEventEvaluationContext evaluation,
        UnknownMapEventSO sourceEvent,
        MapGrid combatGrid,
        Vector2Int playerCell,
        int movesTaken)
    {
        Evaluation = evaluation;
        SourceEvent = sourceEvent;
        CombatGrid = combatGrid;
        PlayerCell = playerCell;
        MovesTaken = movesTaken;
    }

    public UnknownMapEventEvaluationContext Evaluation { get; }
    public UnknownMapEventSO SourceEvent { get; }
    public MapGrid CombatGrid { get; }
    public Vector2Int PlayerCell { get; }
    public int MovesTaken { get; }
}

/// <summary>Polymorphic effect run when the player selects an unknown event option (SerializeReference on <see cref="UnknownMapEventOptionEntry"/>).</summary>
[Serializable]
public abstract class UnknownMapEventOutcomeBase
{
    public abstract void Execute(UnknownMapEventOutcomeContext ctx);
}

/// <summary>Adds run gold (no world pop-up).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeGrantGold : UnknownMapEventOutcomeBase
{
    [Min(0)] public int amount;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (amount <= 0)
            return;
        var eco = RunEconomyManager.TryGetRuntime();
        if (eco == null)
        {
            Debug.LogError("UnknownMapEventOutcomeGrantGold: RunEconomyManager missing.");
            return;
        }

        eco.GrantGold(amount, null);
    }
}

/// <summary>Spends run gold when the player can afford it; otherwise logs and does nothing.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeSpendGold : UnknownMapEventOutcomeBase
{
    [Min(0)] public int amount;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (amount <= 0)
            return;
        var eco = RunEconomyManager.TryGetRuntime();
        if (eco == null)
        {
            Debug.LogError("UnknownMapEventOutcomeSpendGold: RunEconomyManager missing.");
            return;
        }

        if (!eco.TrySpend(amount))
            Debug.LogWarning($"UnknownMapEventOutcomeSpendGold: could not spend {amount} (current {eco.CurrentGold}).");
    }
}

/// <summary>Starts a fight using <see cref="UnknownMapEventSO.ResolveEnemyForCombat"/> and this event’s boss flag.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeStartCombat : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var rm = RunManager.Instance;
        if (rm == null || !rm.UseMapBasedRun)
        {
            Debug.LogError("UnknownMapEventOutcomeStartCombat: invalid RunManager or not map run.");
            return;
        }

        if (ctx.SourceEvent == null || ctx.CombatGrid == null)
        {
            Debug.LogError("UnknownMapEventOutcomeStartCombat: missing source event or grid.");
            return;
        }

        var enemy = ctx.SourceEvent.ResolveEnemyForCombat(rm);
        if (enemy == null)
        {
            Debug.LogError("UnknownMapEventOutcomeStartCombat: no enemy resolved.", ctx.SourceEvent);
            return;
        }

        rm.PersistAndLoadFightSceneWithEnemy(
            ctx.CombatGrid,
            ctx.PlayerCell,
            ctx.MovesTaken,
            enemy,
            ctx.SourceEvent.countsAsBossTileForRunProgression);
    }
}

/// <summary>Same as shrine max-power bonus (map HUD budget).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeApplyShrineMaxPowerBonus : UnknownMapEventOutcomeBase
{
    [Min(1)] public int amount = 2;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.ApplyShrineMaxPowerBonus(amount);
    }
}

/// <summary>Sets current run HP to max (map runs).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeHealRunToFull : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.HealRunVitalityToFull();
    }
}

/// <summary>Applies a delta to current run HP (negative = damage; 0 HP ends the run).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeApplyRunCurrentHpDelta : UnknownMapEventOutcomeBase
{
    public int hpDelta;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.ApplyRunCurrentHpDelta(hpDelta);
    }
}

/// <summary>Changes run max HP only (current HP is clamped to the new max).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeApplyRunMaxHpDelta : UnknownMapEventOutcomeBase
{
    public int maxHpDelta;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.ApplyRunMaxHpDelta(maxHpDelta);
    }
}

/// <summary>Raises run max HP and heals current by the same positive amount.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeApplyRunMaxHpIncreaseAndHeal : UnknownMapEventOutcomeBase
{
    [Min(1)] public int amount = 1;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.ApplyRunMaxHpIncreaseAndHeal(amount);
    }
}

/// <summary>Removes the first die in the deck matching <see cref="DieType"/>.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeRemoveFirstDeckDieOfType : UnknownMapEventOutcomeBase
{
    public DieType dieType;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var pdc = PlayerDataContainer.Instance;
        if (pdc == null)
        {
            Debug.LogError("UnknownMapEventOutcomeRemoveFirstDeckDieOfType: PlayerDataContainer missing.");
            return;
        }

        if (!pdc.TryRemoveFirstDieOfTypeFromDeck(dieType))
            Debug.LogWarning($"UnknownMapEventOutcomeRemoveFirstDeckDieOfType: no die of type {dieType} in deck.");
    }
}

/// <summary>Removes one random die from the run deck.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeRemoveRandomDeckDie : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var pdc = PlayerDataContainer.Instance;
        if (pdc == null)
        {
            Debug.LogError("UnknownMapEventOutcomeRemoveRandomDeckDie: PlayerDataContainer missing.");
            return;
        }

        if (!pdc.TryRemoveRandomDieFromDeck())
            Debug.LogWarning("UnknownMapEventOutcomeRemoveRandomDeckDie: deck had no dice to remove.");
    }
}

/// <summary>Weighted random pick: executes exactly one child outcome.</summary>
[Serializable]
public sealed class UnknownMapEventRandomBranchSlot
{
    [Min(0)] public int weight;
    [SerializeReference] public UnknownMapEventOutcomeBase outcome;
}

[Serializable]
public sealed class UnknownMapEventOutcomeRandomWeightedBranch : UnknownMapEventOutcomeBase
{
    [SerializeField]
    private List<UnknownMapEventRandomBranchSlot> branches = new List<UnknownMapEventRandomBranchSlot>();

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (branches == null || branches.Count == 0)
        {
            Debug.LogError("UnknownMapEventOutcomeRandomWeightedBranch: no branches configured.");
            return;
        }

        var total = 0;
        for (var i = 0; i < branches.Count; i++)
        {
            var s = branches[i];
            if (s != null)
                total += Mathf.Max(0, s.weight);
        }

        if (total <= 0)
        {
            Debug.LogError("UnknownMapEventOutcomeRandomWeightedBranch: total weight is 0.");
            return;
        }

        var r = UnityEngine.Random.Range(0, total);
        for (var i = 0; i < branches.Count; i++)
        {
            var s = branches[i];
            if (s == null)
                continue;
            var w = Mathf.Max(0, s.weight);
            if (r < w)
            {
                s.outcome?.Execute(ctx);
                return;
            }

            r -= w;
        }
    }
}

/// <summary>Registers a completed unknown event id (defaults to <see cref="UnknownMapEventSO.ResolvedEventId"/>).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeMarkEventCompleted : UnknownMapEventOutcomeBase
{
    [Tooltip("Leave empty to use the source event’s ResolvedEventId.")]
    public string explicitEventId;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var id = !string.IsNullOrWhiteSpace(explicitEventId)
            ? explicitEventId.Trim()
            : (ctx.SourceEvent != null ? ctx.SourceEvent.ResolvedEventId : string.Empty);
        RunManager.Instance?.RegisterUnknownMapEventCompleted(id);
    }
}

/// <summary>Runs child outcomes in order.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeComposite : UnknownMapEventOutcomeBase
{
    [SerializeReference]
    private List<UnknownMapEventOutcomeBase> steps = new List<UnknownMapEventOutcomeBase>();

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (steps == null)
            return;
        for (var i = 0; i < steps.Count; i++)
            steps[i]?.Execute(ctx);
    }
}
