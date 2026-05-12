using UnityEngine;

public enum DieType
{
    Damage, // Renamed from Shadow
    Armor,  // Renamed from Defense
    Fire,
    Ice,
    Nature,
    /// <summary>Wildcard socketing (matches any die). Uses <see cref="DieFaceSO.selfDamage"/> for the stored-actions pool and self-hit on turn submit.</summary>
    Curse,
}

public enum FaceRarity
{
    Common,
    Rare,
    Legendary,
}