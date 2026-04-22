/// <summary>One step in a gem's effect list (each row has a single <see cref="GemEffectEntry.param"/>).</summary>
public enum GemEffectKind
{
    HealPlayer,
    GrantExtraRollsThisTurn,
    /// <summary>Burn stacks; set <see cref="GemEffectEntry.burnDefinition"/> on the same entry.</summary>
    ApplyBurnToEnemy,
    /// <summary>Add to the power meter (can exceed max toward bust).</summary>
    IncreasePowerMeter,
    /// <summary>+param rolls this turn; at most 3 activations per player turn (legendary chain cap).</summary>
    BonusRollsThisTurnCapped,
    CleanseRandomDebuff,
    GrantGold,
    AddMaxHpOnly,
    AddArmorToThisFace,
    AddDamageToThisFace,
    /// <summary>param = damage multiplier for this resolve (2 = double, 3 = triple).</summary>
    MultiplyPhysicalDamageThisFace,
    /// <summary>param = number of combat-wide free roll refunds when this die is in a batch (see combat).</summary>
    FreePlayerRollsForThisDie
}
