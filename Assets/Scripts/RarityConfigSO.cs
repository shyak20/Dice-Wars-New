using UnityEngine;

[CreateAssetMenu(fileName = "RarityConfig", menuName = "DiceGame/RarityConfig")]
public class RarityConfigSO : ScriptableObject
{
    [Header("Global Spawn Weights")]
    public int commonWeight = 100;
    public int rareWeight = 25;
    public int legendaryWeight = 5;

    public int GetWeight(FaceRarity rarity)
    {
        return rarity switch
        {
            FaceRarity.Common => commonWeight,
            FaceRarity.Rare => rareWeight,
            FaceRarity.Legendary => legendaryWeight,
            _ => 0
        };
    }
}
