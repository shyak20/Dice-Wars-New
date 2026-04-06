using UnityEngine;

public enum DieType
{
    Damage, // Renamed from Shadow
    Armor,  // Renamed from Defense
    Fire,
    Ice,
    Nature,
}

public enum FaceRarity
{
    Common,
    Rare,
    Legendary,
    /// <summary>Mid tier (pricing / shop); append-only so existing Common/Rare/Legendary assets keep indices 0–2.</summary>
    Uncommon,
    Epic,
}