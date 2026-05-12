using System.Collections.Generic;
using UnityEngine;

public class FaceResult
{
    public DieFaceSO Face { get; set; }
    public int Value { get; set; }
    public DieType Type { get; set; }

    /// <summary>Optional: physical die instance that produced this result (used for reroll picking).</summary>
    public Transform DieSource { get; set; }

    /// <summary>Power actually added for this face (0 when Echo skips power for the batch).</summary>
    public int PowerContributionThisResolve { get; set; }

    /// <summary>How much <see cref="CombatManager"/> kinetic shield bonus was incremented for this resolve (0 or 1).</summary>
    public int KineticShieldBonusContribution { get; set; }

    // New fields to track independent values and timing
    /// <summary>Per-hit physical after Strength, watchers, relics, and face modifiers.</summary>
    public int Damage { get; set; }
    /// <summary>For <see cref="DieType.Damage"/> faces: number of times <see cref="Damage"/> applies (default 1).</summary>
    public int DamageAttackTimes { get; set; } = 1;
    /// <summary>Total physical from this resolve: <see cref="Damage"/> × <see cref="DamageAttackTimes"/> when type is Damage and <see cref="Damage"/> &gt; 0.</summary>
    public int TotalDamageContribution => Type == DieType.Damage && Damage > 0
        ? Damage * Mathf.Max(1, DamageAttackTimes)
        : 0;

    public int Armor { get; set; }

    public int SelfDamage { get; set; }

    /// <summary>Pool row total for curse self-hit (not multiplied by attack times).</summary>
    public int TotalSelfDamageContribution => Type == DieType.Curse ? Mathf.Max(0, SelfDamage) : 0;

    /// <summary>Set when this resolve is the next Fire face after <see cref="TurnRegistry.PendingNextFireRollDoubleEnemyBurn"/>; doubles enemy burn stacks applied from this face only.</summary>
    public bool DoubleEnemyBurnStacksThisResolve { get; set; }

    /// <summary>When <see cref="DoubleEnemyBurnStacksThisResolve"/> and <paramref name="appliedStatus"/> is enemy-target burn, returns <paramref name="stacks"/> × 2; otherwise unchanged.</summary>
    public int ApplyFireDoubleToEnemyBurnStacks(int stacks, StatusEffectSO appliedStatus)
    {
        if (stacks <= 0 || appliedStatus == null || !DoubleEnemyBurnStacksThisResolve) return stacks;
        if (appliedStatus is BurnEffectSO b && b.target == StatusEffectTarget.Enemy) return stacks * 2;
        return stacks;
    }

    /// <summary>Overload for burn-only apply paths (e.g. roll watcher).</summary>
    public int ApplyFireDoubleToEnemyBurnStacks(int stacks, BurnEffectSO burnDefinition)
    {
        if (burnDefinition == null || stacks <= 0 || !DoubleEnemyBurnStacksThisResolve) return stacks;
        if (burnDefinition.target == StatusEffectTarget.Enemy) return stacks * 2;
        return stacks;
    }

    /// <summary>Copied from the rolled face; <see cref="IGameAction.ActivateImmediately"/> controls gather vs turn-end <see cref="IGameAction.Execute"/>, and early vs late <see cref="FaceResolveModifierBase.Modify"/>.</summary>
    public List<IGameAction> Actions { get; set; } = new List<IGameAction>();

    /// <summary>Deferred-action rows for <see cref="StoredActionsPoolDisplay"/> (ApplyStatusEffect, Thorns, Max HP, etc.); filled before this face is added to channeled faces.</summary>
    public List<FacePoolExtraContribution> ActionPoolContributions { get; } = new List<FacePoolExtraContribution>();
}
