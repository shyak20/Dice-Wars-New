using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "RelicLootTable", menuName = "DiceGame/Relics/Relic Loot Table")]
public class RelicLootTableSO : ScriptableObject
{
    public RarityConfigSO rarityConfig;
    public List<RelicSO> allPossibleRelics = new List<RelicSO>();

    public List<RelicSO> GetRandomRelics(int count)
    {
        var selected = new List<RelicSO>();
        if (allPossibleRelics == null || allPossibleRelics.Count == 0)
        {
            Debug.LogError("RelicLootTableSO: allPossibleRelics is empty.", this);
            return selected;
        }

        if (rarityConfig == null)
        {
            Debug.LogError("RelicLootTableSO: assign rarityConfig.", this);
            return selected;
        }

        var pool = allPossibleRelics.Where(r => r != null).ToList();
        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var totalWeight = pool.Sum(r => rarityConfig.GetWeight(r.rarity));
            var roll = UnityEngine.Random.Range(0, totalWeight);
            var acc = 0;
            foreach (var r in pool)
            {
                acc += rarityConfig.GetWeight(r.rarity);
                if (roll < acc)
                {
                    selected.Add(r);
                    pool.Remove(r);
                    break;
                }
            }
        }

        return selected;
    }
}
