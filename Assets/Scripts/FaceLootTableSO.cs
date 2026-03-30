using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "NewLootTable", menuName = "DiceGame/LootTable")]
public class FaceLootTableSO : ScriptableObject
{
    public RarityConfigSO rarityConfig;
    public List<DieFaceSO> allPossibleFaces;

    public List<DieFaceSO> GetRandomRewards(int count, HashSet<DieType> preferredTypes = null)
    {
        List<DieFaceSO> selected = new List<DieFaceSO>();
        if (allPossibleFaces == null || allPossibleFaces.Count == 0 || rarityConfig == null) return selected;

        List<DieFaceSO> pool;
        if (preferredTypes != null && preferredTypes.Count > 0)
        {
            pool = allPossibleFaces.Where(f => preferredTypes.Contains(f.type)).ToList();
            if (pool.Count < count)
                pool = new List<DieFaceSO>(allPossibleFaces);
        }
        else
        {
            if (preferredTypes != null)
            {
                Debug.LogError($"Unable to find enough faces ({count}) for {string.Join(", ", preferredTypes)}");
            }
            
            pool = new List<DieFaceSO>(allPossibleFaces);
        }

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            // Sum weights based on the global rarity config
            int totalWeight = pool.Sum(f => rarityConfig.GetWeight(f.rarity));
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var face in pool)
            {
                currentWeight += rarityConfig.GetWeight(face.rarity);
                if (roll < currentWeight)
                {
                    selected.Add(face);
                    pool.Remove(face);
                    break;
                }
            }
        }
        return selected;
    }
}