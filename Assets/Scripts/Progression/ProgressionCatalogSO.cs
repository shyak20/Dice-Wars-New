using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProgressionCatalog", menuName = "DiceGame/Progression/Progression Catalog")]
public class ProgressionCatalogSO : ScriptableObject
{
    [Tooltip("Ordered rank ladder (Novice = 0, etc.). Assign this catalog on the owning PlayerDataSO.progressionCatalog.")]
    public List<PlayerRankSO> ranks = new List<PlayerRankSO>();

    [Tooltip("Face/gem/relic/die ids always eligible in loot pools even before horizontal unlocks.")]
    public List<string> baseAlwaysAvailableIds = new List<string>();

    public bool TryGetRank(int rankIndex, out PlayerRankSO rank)
    {
        rank = null;
        if (ranks == null)
            return false;

        for (var i = 0; i < ranks.Count; i++)
        {
            var r = ranks[i];
            if (r != null && r.rankIndex == rankIndex)
            {
                rank = r;
                return true;
            }
        }

        return false;
    }

    public PlayerRankSO GetRankOrNull(int rankIndex)
    {
        TryGetRank(rankIndex, out var rank);
        return rank;
    }

    public bool TryGetNextRank(int currentRankIndex, out PlayerRankSO next)
    {
        next = null;
        var best = int.MaxValue;
        PlayerRankSO candidate = null;

        if (ranks == null)
            return false;

        for (var i = 0; i < ranks.Count; i++)
        {
            var r = ranks[i];
            if (r == null || r.rankIndex <= currentRankIndex)
                continue;
            if (r.rankIndex < best)
            {
                best = r.rankIndex;
                candidate = r;
            }
        }

        if (candidate == null)
            return false;

        next = candidate;
        return true;
    }

    public int MaxRankIndex()
    {
        var max = 0;
        if (ranks == null)
            return 0;

        for (var i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] != null)
                max = Mathf.Max(max, ranks[i].rankIndex);
        }

        return max;
    }

    public bool IsAlwaysAvailable(string contentId)
    {
        if (ProgressionContentIds.IsNullOrEmpty(contentId) || baseAlwaysAvailableIds == null)
            return false;

        for (var i = 0; i < baseAlwaysAvailableIds.Count; i++)
        {
            if (string.Equals(baseAlwaysAvailableIds[i], contentId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    void OnValidate()
    {
        if (ranks == null)
            return;

        ranks.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.rankIndex.CompareTo(b.rankIndex);
        });
    }
}
