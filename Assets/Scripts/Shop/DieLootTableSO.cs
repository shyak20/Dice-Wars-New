using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pool of all dice that can appear in the shop (like <see cref="FaceLootTableSO"/> for faces).
/// </summary>
[CreateAssetMenu(fileName = "NewDieLootTable", menuName = "DiceGame/DieLootTable")]
public class DieLootTableSO : ScriptableObject
{
    [Tooltip("All dice the shop can roll from.")]
    public List<DieAssetSO> allPossibleDice = new List<DieAssetSO>();

    /// <summary>
    /// Picks one random die. <paramref name="preferredChance"/> of the time, prefers dice matching the player's deck elements.
    /// </summary>
    public DieAssetSO GetRandomDie(HashSet<DieType> preferredTypes = null, float preferredChance = 0.7f)
    {
        var batch = GetRandomDice(1, preferredTypes, preferredChance, uniqueInBatch: false);
        return batch.Count > 0 ? batch[0] : null;
    }

    /// <summary>
    /// Picks up to <paramref name="count"/> dice. If <paramref name="uniqueInBatch"/> is true, the same die is not offered twice in one shop restock.
    /// </summary>
    public List<DieAssetSO> GetRandomDice(int count, HashSet<DieType> preferredTypes = null, float preferredChance = 0.7f, bool uniqueInBatch = true) =>
        GetRandomDiceFromPool(count, allPossibleDice, preferredTypes, preferredChance, uniqueInBatch);

    public List<DieAssetSO> GetRandomDiceFromPool(
        int count,
        List<DieAssetSO> candidatePool,
        HashSet<DieType> preferredTypes = null,
        float preferredChance = 0.7f,
        bool uniqueInBatch = true)
    {
        var result = new List<DieAssetSO>();
        if (count <= 0) return result;
        if (candidatePool == null || candidatePool.Count == 0) return result;

        var pool = candidatePool.Where(d => d != null).ToList();
        if (pool.Count == 0) return result;

        for (var i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            var usePreferred = preferredTypes != null && preferredTypes.Count > 0 && Random.value < preferredChance;
            List<DieAssetSO> pickPool;
            if (usePreferred)
            {
                pickPool = pool.Where(d => preferredTypes.Contains(d.dieType)).ToList();
                if (pickPool.Count == 0) pickPool = pool;
            }
            else
            {
                pickPool = pool;
            }

            var chosen = pickPool[Random.Range(0, pickPool.Count)];
            result.Add(chosen);
            if (uniqueInBatch)
                pool.Remove(chosen);
        }

        return result;
    }
}
