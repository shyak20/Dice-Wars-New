using System.Collections.Generic;
using UnityEngine;

/// <summary>Restricts loot tables to horizontally unlocked content plus catalog base pool.</summary>
public static class ProgressionLootFilter
{
    public static List<DieFaceSO> FilterFaces(IReadOnlyList<DieFaceSO> pool, ProgressionCatalogSO catalog, ProgressionManager manager)
    {
        if (pool == null || pool.Count == 0)
            return new List<DieFaceSO>();

        if (manager == null || !manager.HasHorizontalUnlockGates())
            return CopyNonNull(pool);

        var result = new List<DieFaceSO>();
        for (var i = 0; i < pool.Count; i++)
        {
            var face = pool[i];
            if (face == null)
                continue;
            if (IsAllowed(ProgressionContentIds.ForFace(face), catalog, manager))
                result.Add(face);
        }

        return result;
    }

    public static List<GemSO> FilterGems(IReadOnlyList<GemSO> pool, ProgressionCatalogSO catalog, ProgressionManager manager)
    {
        if (pool == null || pool.Count == 0)
            return new List<GemSO>();

        if (manager == null || !manager.HasHorizontalUnlockGates())
            return CopyNonNull(pool);

        var result = new List<GemSO>();
        for (var i = 0; i < pool.Count; i++)
        {
            var gem = pool[i];
            if (gem == null)
                continue;
            if (IsAllowed(ProgressionContentIds.ForGem(gem), catalog, manager))
                result.Add(gem);
        }

        return result;
    }

    public static List<RelicSO> FilterRelics(IReadOnlyList<RelicSO> pool, ProgressionCatalogSO catalog, ProgressionManager manager)
    {
        if (pool == null || pool.Count == 0)
            return new List<RelicSO>();

        if (manager == null || !manager.HasHorizontalUnlockGates())
            return CopyNonNull(pool);

        var result = new List<RelicSO>();
        for (var i = 0; i < pool.Count; i++)
        {
            var relic = pool[i];
            if (relic == null)
                continue;
            if (IsAllowed(ProgressionContentIds.ForRelic(relic), catalog, manager))
                result.Add(relic);
        }

        return result;
    }

    public static List<DieAssetSO> FilterDice(IReadOnlyList<DieAssetSO> pool, ProgressionCatalogSO catalog, ProgressionManager manager)
    {
        if (pool == null || pool.Count == 0)
            return new List<DieAssetSO>();

        if (manager == null || !manager.HasHorizontalUnlockGates())
            return CopyNonNull(pool);

        var result = new List<DieAssetSO>();
        for (var i = 0; i < pool.Count; i++)
        {
            var die = pool[i];
            if (die == null)
                continue;
            if (IsAllowed(ProgressionContentIds.ForDie(die), catalog, manager))
                result.Add(die);
        }

        return result;
    }

    static bool IsAllowed(string contentId, ProgressionCatalogSO catalog, ProgressionManager manager)
    {
        if (catalog != null && catalog.IsAlwaysAvailable(contentId))
            return true;
        return manager.IsContentUnlocked(contentId);
    }

    static List<T> CopyNonNull<T>(IReadOnlyList<T> pool) where T : Object
    {
        var list = new List<T>();
        for (var i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null)
                list.Add(pool[i]);
        }

        return list;
    }
}
