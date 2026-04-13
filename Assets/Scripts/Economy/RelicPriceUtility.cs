using UnityEngine;

public static class RelicPriceUtility
{
    public static int GetRelicGoldPrice(RelicSO relic)
    {
        if (relic == null) return 0;
        if (relic.shopGoldPrice > 0)
            return relic.shopGoldPrice;
        return 50 * FacePriceUtility.GetRarityModifier(relic.rarity) / 10;
    }
}
