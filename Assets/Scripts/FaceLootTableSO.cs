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
        if (allPossibleFaces == null || allPossibleFaces.Count == 0)
        {
            Debug.LogError("FaceLootTableSO: allPossibleFaces is empty — assign faces on the loot table asset.");
            return selected;
        }

        if (rarityConfig == null)
        {
            Debug.LogError("FaceLootTableSO: rarityConfig is not assigned — shop and rewards cannot roll faces without it.");
            return selected;
        }

        var validFaces = allPossibleFaces.FindAll(f => f != null);
        if (validFaces.Count == 0)
        {
            Debug.LogError("FaceLootTableSO: allPossibleFaces contains only null entries — fix the loot table asset.");
            return selected;
        }

        List<DieFaceSO> pool;
        if (preferredTypes != null && preferredTypes.Count > 0)
        {
            pool = validFaces.Where(f => preferredTypes.Contains(f.type)).ToList();
            if (pool.Count < count)
                pool = new List<DieFaceSO>(validFaces);
        }
        else
        {
            if (preferredTypes != null)
            {
                Debug.LogError($"Unable to find enough faces ({count}) for {string.Join(", ", preferredTypes)}");
            }

            pool = new List<DieFaceSO>(validFaces);
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

    /// <summary>Faces from <see cref="allPossibleFaces"/> with the given rarity that <see cref="DieFaceSO.MatchesDie"/> the die.</summary>
    public List<DieFaceSO> GetCandidatesForDieAndRarity(DieAssetSO die, FaceRarity rarity)
    {
        var candidates = new List<DieFaceSO>();
        if (die == null || allPossibleFaces == null || allPossibleFaces.Count == 0)
            return candidates;

        for (var i = 0; i < allPossibleFaces.Count; i++)
        {
            var face = allPossibleFaces[i];
            if (face != null && face.rarity == rarity && face.MatchesDie(die))
                candidates.Add(face);
        }

        return candidates;
    }

    /// <summary>Uniform random pick from <see cref="GetCandidatesForDieAndRarity"/>; null when none match.</summary>
    public DieFaceSO PickRandomForDieAndRarity(DieAssetSO die, FaceRarity rarity)
    {
        var candidates = GetCandidatesForDieAndRarity(die, rarity);
        if (candidates.Count == 0)
            return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }
}