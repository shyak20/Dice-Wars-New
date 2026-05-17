/// <summary>
/// Bonus channel for combat face actions (e.g. <see cref="BonusPerOwnedDieAction"/>, <see cref="ResonanceMeterBonusModifier"/>).
/// Fire applies burn stacks via an assigned <see cref="BurnEffectSO"/>.
/// </summary>
public enum CombatBonusChannel
{
    Armor,
    Damage,
    Fire,
    Poison,
}
