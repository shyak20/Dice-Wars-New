using UnityEngine;

/// <summary>Centralized, configurable pricing chart used by <see cref="UIShopWindow"/>.</summary>
public class ShopPricingManager : MonoBehaviour
{
    [Header("Die Face Prices")]
    [Min(0)] [SerializeField] private int dieFaceCommonPrice = 40;
    [Min(0)] [SerializeField] private int dieFaceRarePrice = 80;
    [Min(0)] [SerializeField] private int dieFaceLegendaryPrice = 120;

    [Header("Gem Prices - Level 1")]
    [Min(0)] [SerializeField] private int gemCommonLevel1Price = 120;
    [Min(0)] [SerializeField] private int gemRareLevel1Price = 160;
    [Min(0)] [SerializeField] private int gemLegendaryLevel1Price = 200;

    [Header("Gem Prices - Level 2")]
    [Min(0)] [SerializeField] private int gemCommonLevel2Price = 240;
    [Min(0)] [SerializeField] private int gemRareLevel2Price = 320;
    [Min(0)] [SerializeField] private int gemLegendaryLevel2Price = 400;

    [Header("Gem Prices - Level 3")]
    [Min(0)] [SerializeField] private int gemCommonLevel3Price = 360;
    [Min(0)] [SerializeField] private int gemRareLevel3Price = 480;
    [Min(0)] [SerializeField] private int gemLegendaryLevel3Price = 600;

    [Header("Artifact (Relic) Prices")]
    [Min(0)] [SerializeField] private int relicCommonPrice = 160;
    [Min(0)] [SerializeField] private int relicRarePrice = 200;
    [Min(0)] [SerializeField] private int relicLegendaryPrice = 240;

    [Header("Die Price")]
    [Min(0)] [SerializeField] private int diePrice = 250;

    public int GetDieFacePrice(DieFaceSO face)
    {
        if (face == null) return 0;
        return face.rarity switch
        {
            FaceRarity.Common => dieFaceCommonPrice,
            FaceRarity.Rare => dieFaceRarePrice,
            FaceRarity.Legendary => dieFaceLegendaryPrice,
            _ => dieFaceCommonPrice
        };
    }

    public int GetGemPrice(GemSO gem)
    {
        if (gem == null) return 0;
        var level = Mathf.Clamp(gem.level, 1, 3);
        return (gem.rarity, level) switch
        {
            (FaceRarity.Common, 1) => gemCommonLevel1Price,
            (FaceRarity.Rare, 1) => gemRareLevel1Price,
            (FaceRarity.Legendary, 1) => gemLegendaryLevel1Price,
            (FaceRarity.Common, 2) => gemCommonLevel2Price,
            (FaceRarity.Rare, 2) => gemRareLevel2Price,
            (FaceRarity.Legendary, 2) => gemLegendaryLevel2Price,
            (FaceRarity.Common, 3) => gemCommonLevel3Price,
            (FaceRarity.Rare, 3) => gemRareLevel3Price,
            (FaceRarity.Legendary, 3) => gemLegendaryLevel3Price,
            _ => gemCommonLevel1Price
        };
    }

    public int GetRelicPrice(RelicSO relic)
    {
        if (relic == null) return 0;
        return relic.rarity switch
        {
            FaceRarity.Common => relicCommonPrice,
            FaceRarity.Rare => relicRarePrice,
            FaceRarity.Legendary => relicLegendaryPrice,
            _ => relicCommonPrice
        };
    }

    public int GetDiePrice(DieAssetSO die) => die != null ? diePrice : 0;
}
