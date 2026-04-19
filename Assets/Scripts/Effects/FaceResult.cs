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
    public int Damage { get; set; }
    public int Armor { get; set; }
    public bool ActivateImmediately { get; set; }
    /// <summary>Copied from the rolled face; executed in order (immediate vs turn-end per <see cref="ActivateImmediately"/>).</summary>
    public List<IGameAction> Actions { get; set; } = new List<IGameAction>();

    /// <summary>Per-action element pool rows (Burn→Fire, Poison→Nature, etc.); filled before <see cref="CombatManager"/> adds this to channeled faces.</summary>
    public List<FacePoolExtraContribution> ActionPoolContributions { get; } = new List<FacePoolExtraContribution>();
}
