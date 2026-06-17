using UnityEngine;

/// <summary>Legacy helper: fixed face prices by rarity.</summary>
public static class FacePriceUtility
{
    public static int GetRarityModifier(FaceRarity rarity)
    {
        return rarity switch
        {
            FaceRarity.Common => 40,
            FaceRarity.Rare => 80,
            FaceRarity.Legendary => 120,
            _ => 40
        };
    }

    public static int GetFaceGoldPrice(DieFaceSO face)
    {
        if (face == null) return 0;
        return GetRarityModifier(face.rarity);
    }
}
