using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ruby shard costs and constant stat increments for per-character meta upgrades.
/// Each price list length defines the maximum number of purchases for that upgrade track.
/// </summary>
[CreateAssetMenu(fileName = "Character Meta Upgrade Prices", menuName = "DiceGame/Meta/Character Meta Upgrade Prices")]
public sealed class CharacterMetaUpgradePricesSO : ScriptableObject
{
    [Header("Upgrade increments (same for every tier)")]
    [SerializeField, Min(1)] private int healthIncreasePerUpgrade = 5;
    [SerializeField, Min(1)] private int maxPowerIncreasePerUpgrade = 1;

    [Header("Ruby shard cost per tier (list length = max upgrades)")]
    [SerializeField] private List<int> healthUpgradePrices = new List<int>();
    [SerializeField] private List<int> maxPowerUpgradePrices = new List<int>();
    [SerializeField] private List<int> startingDiceUnlockPrices = new List<int>();

    public int HealthIncreasePerUpgrade => healthIncreasePerUpgrade;
    public int MaxPowerIncreasePerUpgrade => maxPowerIncreasePerUpgrade;

    public int MaxHealthUpgrades => healthUpgradePrices != null ? healthUpgradePrices.Count : 0;
    public int MaxMaxPowerUpgrades => maxPowerUpgradePrices != null ? maxPowerUpgradePrices.Count : 0;
    public int MaxStartingDiceUnlocks => startingDiceUnlockPrices != null ? startingDiceUnlockPrices.Count : 0;

    public bool TryGetHealthUpgradePrice(int currentLevel, out int price) =>
        TryGetPriceAtLevel(healthUpgradePrices, currentLevel, out price);

    public bool TryGetMaxPowerUpgradePrice(int currentLevel, out int price) =>
        TryGetPriceAtLevel(maxPowerUpgradePrices, currentLevel, out price);

    public bool TryGetStartingDiceUnlockPrice(int currentLevel, out int price) =>
        TryGetPriceAtLevel(startingDiceUnlockPrices, currentLevel, out price);

    static bool TryGetPriceAtLevel(List<int> prices, int currentLevel, out int price)
    {
        price = 0;
        if (prices == null || currentLevel < 0 || currentLevel >= prices.Count)
            return false;

        price = Mathf.Max(0, prices[currentLevel]);
        return true;
    }
}
