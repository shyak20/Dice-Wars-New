using System.Collections.Generic;

public class FaceResult
{
    public DieFaceSO Face { get; set; }
    public int Value { get; set; }
    public DieType Type { get; set; }

    // New fields to track independent values and timing
    public int Damage { get; set; }
    public int Armor { get; set; }
    public bool ActivateImmediately { get; set; }
    /// <summary>Copied from the rolled face; executed in order (immediate vs turn-end per <see cref="ActivateImmediately"/>).</summary>
    public List<IGameAction> Actions { get; set; } = new List<IGameAction>();

    /// <summary>Per-action element pool rows (Burn→Fire, Poison→Nature, etc.); filled before <see cref="CombatManager"/> adds this to channeled faces.</summary>
    public List<FacePoolExtraContribution> ActionPoolContributions { get; } = new List<FacePoolExtraContribution>();
}
