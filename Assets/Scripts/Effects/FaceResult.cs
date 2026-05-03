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

    /// <summary>Portion of <see cref="TotalDamageContribution"/> already applied to the enemy via early flush (bust revert uses this).</summary>
    public int PhysicalDamageAppliedEarly { get; set; }

    /// <summary>Portion of <see cref="Armor"/> already granted to the player via early flush.</summary>
    public int ArmorAppliedEarly { get; set; }

    public bool ActivateImmediately { get; set; }
    /// <summary>Copied from the rolled face; executed in order (immediate vs turn-end per <see cref="ActivateImmediately"/>).</summary>
    public List<IGameAction> Actions { get; set; } = new List<IGameAction>();

    /// <summary>Deferred-action rows for <see cref="StoredActionsPoolDisplay"/> (ApplyStatusEffect, Thorns, Max HP, etc.); filled before this face is added to channeled faces.</summary>
    public List<FacePoolExtraContribution> ActionPoolContributions { get; } = new List<FacePoolExtraContribution>();

    /// <summary>Enemy-only applies run when the matching flyout line lands (cleared when attached to <see cref="DiceRollVisualPayload"/>).</summary>
    public List<DeferredEnemyApplyEntry> DeferredEnemyFlyoutApplies;
}
