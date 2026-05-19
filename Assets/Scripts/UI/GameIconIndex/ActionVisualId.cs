/// <summary>Keys into <see cref="GameIconIndexSO"/> for dice face actions (not status effects).</summary>
public enum ActionVisualId
{
    None = 0,
    Heal = 1,
    Cleanse = 2,
    Overcharge = 3,
    /// <summary>Reserved icon slot (was legacy Echo face action). Use <see cref="EchoEffectSO"/> + <see cref="ApplyStatusEffectAction"/> instead.</summary>
    LegacyEchoActionIconSlot = 4,
    KineticShield = 5,
    Precision = 6,
    /// <summary>Reserved icon slot (was legacy Immune face action). Use <see cref="ImmuneEffectSO"/> + <see cref="ApplyStatusEffectAction"/>.</summary>
    LegacyImmuneActionIconSlot = 7,
    Broken = 8,
    SafetyNet = 9,
    Thorns = 10,
    MaxHp = 11,
    FirstRollAction = 12,
    AddRolls = 13,
    AddValueBasedOnRoll = 14,
    RerollDie = 15,
    /// <summary>Optional +X power prompt (<see cref="AddPowerAction"/>).</summary>
    AddPower = 16,

    /// <summary><see cref="DamageFromEnemyBurnStacksPercentAction"/> — pending damage from % of enemy burn stacks.</summary>
    DamageFromEnemyBurnStacks = 17,

    /// <summary><see cref="InstantBurnDamageFromEnemyStacksPercentAction"/> — immediate burn damage from % of stacks (stacks unchanged).</summary>
    InstantBurnProcFromStacks = 18,

    /// <summary><see cref="ConsumeAllBurnForMaxHpAction"/> — strip burn from a target for +max HP.</summary>
    ConsumeBurnForMaxHp = 19,

    /// <summary><see cref="ArmorFromEnemyBurnStacksAction"/> — bonus armor from % of enemy burn stacks.</summary>
    ArmorFromEnemyBurnStacks = 20,

    /// <summary><see cref="BonusArmorBurnWhenEnemyHitsArmorAction"/> — armor + burn when enemy breaks player armor.</summary>
    BonusArmorBurnWhenStruck = 21,

    /// <summary><see cref="BonusDamageIfEnemyBurnMeetsThresholdAction"/> — bonus damage scaled when enemy burn ≥ threshold.</summary>
    BonusDamageVsBurnThreshold = 22,

    /// <summary><see cref="StartNextTurnWithArmorAction"/> — next player turn begins with X armor.</summary>
    StartNextTurnWithArmor = 23,

    /// <summary><see cref="BonusPerOwnedDieAction"/> — bonus per die in the player's deck (armor, damage, fire, poison).</summary>
    BonusPerOwnedDie = 24,

    /// <summary><see cref="ResonanceMeterBonusModifier"/> — bonus equal to Resonance Meter before this roll resolves.</summary>
    ResonanceMeterBonus = 25,

    /// <summary><see cref="PrimeNextFireRollDoubleEnemyBurnModifier"/> — next Fire roll doubles enemy burn stacks applied.</summary>
    PrimeNextFireRollDoubleEnemyBurn = 26,
}
