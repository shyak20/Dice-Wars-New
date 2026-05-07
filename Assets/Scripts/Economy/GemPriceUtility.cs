using UnityEngine;

public static class GemPriceUtility
{
    public static int GetGemGoldPrice(GemSO gem)
    {
        if (gem == null) return 0;
        var baseByRarity = gem.rarity switch
        {
            FaceRarity.Common => 120,
            FaceRarity.Rare => 160,
            FaceRarity.Legendary => 200,
            _ => 120
        };
        var level = Mathf.Clamp(gem.level, 1, 3);
        return baseByRarity * level;
    }
}
