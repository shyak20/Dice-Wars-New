using UnityEngine;

public static class GemPriceUtility
{
    public static int GetGemGoldPrice(GemSO gem)
    {
        if (gem == null) return 0;
        if (gem.shopGoldPrice > 0)
            return gem.shopGoldPrice;
        return 40 * FacePriceUtility.GetRarityModifier(gem.rarity) / 10;
    }
}
