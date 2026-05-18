/// <summary>One step in a gem's effect list (each row has a single <see cref="GemEffectEntry.param"/>).</summary>
public enum GemEffectKind
{
    HealPlayer,
    GrantExtraRollsThisTurn,
    /// <summary>Burn stacks; set <see cref="GemEffectEntry.burnDefinition"/> on the same entry.</summary>
    ApplyBurnToEnemy,
    /// <summary>Add to the power meter (can exceed max toward bust).</summary>
    IncreasePowerMeter,
    /// <summary>param = count of other dice (later in batch gather order) to throw again for free; their new face adds no power. Max 3 procs per die per roll batch.</summary>
    RandomBatchRerollOtherDiceNoPower,
    CleanseRandomDebuff,
    GrantGold,
    AddMaxHpOnly,
    AddArmorToThisFace,
    AddDamageToThisFace,
    /// <summary>param = damage multiplier for this resolve (2 = double, 3 = triple).</summary>
    MultiplyPhysicalDamageThisFace,
    /// <summary>On matching rolls only: consume one charge and contribute 0 power to the power pool. Param = charges.</summary>
    FreeFirstRollForThisDie,
    /// <summary>Status stacks; set <see cref="GemEffectEntry.statusDefinition"/> and param = stack count.</summary>
    ApplyStatus
}
