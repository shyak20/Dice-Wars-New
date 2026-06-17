using UnityEngine;

public static class RelicPriceUtility
{
    public static int GetRelicGoldPrice(RelicSO relic)
    {
        if (relic == null) return 0;
        return relic.rarity switch
        {
            FaceRarity.Common => 160,
            FaceRarity.Rare => 200,
            FaceRarity.Legendary => 240,
            _ => 160
        };
    }
}
