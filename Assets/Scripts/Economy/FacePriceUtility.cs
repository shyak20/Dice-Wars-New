using UnityEngine;

/// <summary>Shop pricing: Price = Face.Value × RarityModifier.</summary>
public static class FacePriceUtility
{
    public static int GetRarityModifier(FaceRarity rarity)
    {
        return rarity switch
        {
            FaceRarity.Common => 10,
            FaceRarity.Uncommon => 25,
            FaceRarity.Rare => 50,
            FaceRarity.Legendary => 100,
            FaceRarity.Epic => 100,
            _ => 10
        };
    }

    public static int GetFaceGoldPrice(DieFaceSO face)
    {
        if (face == null) return 0;
        return Mathf.Max(0, face.value) * GetRarityModifier(face.rarity);
    }
}
