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
}
