using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Per-character meta upgrade tier limits and constant stat increments.
/// Each tier list length defines the maximum number of upgrades for that track (list values are unused until a future unlock source is added).
/// </summary>
[CreateAssetMenu(fileName = "Character Meta Upgrade Prices", menuName = "DiceGame/Meta/Character Meta Upgrade Prices")]
public sealed class CharacterMetaUpgradePricesSO : ScriptableObject
{
    [Header("Upgrade increments (same for every tier)")]
    [SerializeField, Min(1)] private int healthIncreasePerUpgrade = 5;
    [SerializeField, Min(1)] private int maxPowerIncreasePerUpgrade = 1;

    [Header("Upgrade tiers (list length = max upgrades per track)")]
    [FormerlySerializedAs("healthUpgradePrices")]
    [SerializeField] private List<int> healthUpgradeTiers = new List<int>();
    [FormerlySerializedAs("maxPowerUpgradePrices")]
    [SerializeField] private List<int> maxPowerUpgradeTiers = new List<int>();
    [FormerlySerializedAs("startingDiceUnlockPrices")]
    [SerializeField] private List<int> startingDiceUnlockTiers = new List<int>();

    public int HealthIncreasePerUpgrade => healthIncreasePerUpgrade;
    public int MaxPowerIncreasePerUpgrade => maxPowerIncreasePerUpgrade;

    public int MaxHealthUpgrades => healthUpgradeTiers != null ? healthUpgradeTiers.Count : 0;
    public int MaxMaxPowerUpgrades => maxPowerUpgradeTiers != null ? maxPowerUpgradeTiers.Count : 0;
    public int MaxStartingDiceUnlocks => startingDiceUnlockTiers != null ? startingDiceUnlockTiers.Count : 0;

    public bool HasNextHealthUpgradeTier(int currentLevel) => HasNextTier(healthUpgradeTiers, currentLevel);

    public bool HasNextMaxPowerUpgradeTier(int currentLevel) => HasNextTier(maxPowerUpgradeTiers, currentLevel);

    public bool HasNextStartingDiceUnlockTier(int currentLevel) => HasNextTier(startingDiceUnlockTiers, currentLevel);

    static bool HasNextTier(List<int> tiers, int currentLevel) =>
        tiers != null && currentLevel >= 0 && currentLevel < tiers.Count;
}
