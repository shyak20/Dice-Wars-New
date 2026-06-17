using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "GemLootTable", menuName = "DiceGame/Gems/Gem Loot Table")]
public class GemLootTableSO : ScriptableObject
{
    public RarityConfigSO rarityConfig;
    public List<GemSO> allPossibleGems = new List<GemSO>();

    public List<GemSO> GetRandomGems(int count) => GetRandomGemsFromPool(count, allPossibleGems);

    public List<GemSO> GetRandomGemsFromPool(int count, List<GemSO> candidatePool)
    {
        var selected = new List<GemSO>();
        if (candidatePool == null || candidatePool.Count == 0)
        {
            Debug.LogError("GemLootTableSO: allPossibleGems is empty.", this);
            return selected;
        }

        if (rarityConfig == null)
        {
            Debug.LogError("GemLootTableSO: assign rarityConfig.", this);
            return selected;
        }

        var pool = candidatePool.Where(g => g != null).ToList();
        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var totalWeight = pool.Sum(g => rarityConfig.GetWeight(g.rarity));
            var roll = Random.Range(0, totalWeight);
            var acc = 0;
            foreach (var g in pool)
            {
                acc += rarityConfig.GetWeight(g.rarity);
                if (roll < acc)
                {
                    selected.Add(g);
                    pool.Remove(g);
                    break;
                }
            }
        }

        return selected;
    }
}
