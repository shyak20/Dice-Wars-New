using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One effect row on a gem: kind + single integer param (+ burn asset when burning).</summary>
[Serializable]
public class GemEffectEntry
{
    public GemEffectKind kind;

    [Tooltip("Interpretation depends on Kind (heal amount, burn stacks, power delta, damage multiplier, etc.).")]
    public int param;

    [Tooltip("Required when Kind is ApplyBurnToEnemy.")]
    public BurnEffectSO burnDefinition;
}

/// <summary>
/// Permanent socket upgrade for a <see cref="DieAssetSO"/> instance. Bought in the shop, then attached to a die with an empty socket.
/// </summary>
[CreateAssetMenu(fileName = "NewGem", menuName = "DiceGame/Gems/Gem")]
public class GemSO : ScriptableObject
{
    [Tooltip("Shown in shop / UI; defaults to asset name if empty.")]
    public string displayName;

    [TextArea(2, 5)]
    public string description;

    public Sprite icon;

    public FaceRarity rarity = FaceRarity.Common;

    [Tooltip("Shop gold; if 0, uses rarity-based default from GemPriceUtility.")]
    [Min(0)]
    public int shopGoldPrice;

    [Range(1, 3)]
    public int level = 1;

    [Tooltip("Rolled face values (1–6, after status modifiers) that trigger this gem.")]
    public List<int> matchFaceValues = new List<int>();

    [Tooltip("All entries run in order when the face matches (e.g. burn then power meter as two rows).")]
    public List<GemEffectEntry> effects = new List<GemEffectEntry>();

    public string DisplayLabel => string.IsNullOrEmpty(displayName) ? name : displayName;

    public bool MatchesRolledValue(int faceValue) =>
        matchFaceValues != null && matchFaceValues.Contains(faceValue);
}
