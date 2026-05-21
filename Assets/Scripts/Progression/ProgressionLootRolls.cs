using System.Collections.Generic;

/// <summary>Loot rolls that respect horizontal progression unlocks.</summary>
public static class ProgressionLootRolls
{
    public static List<DieFaceSO> RollFaces(FaceLootTableSO table, int count, HashSet<DieType> preferredTypes)
    {
        if (table == null)
            return new List<DieFaceSO>();

        var mgr = ProgressionManager.TryGetRuntime();
        var pool = mgr != null && mgr.Catalog != null
            ? ProgressionLootFilter.FilterFaces(table.allPossibleFaces, mgr.Catalog, mgr)
            : table.allPossibleFaces;

        return table.GetRandomRewardsFromPool(count, preferredTypes, pool);
    }

    public static List<GemSO> RollGems(GemLootTableSO table, int count)
    {
        if (table == null)
            return new List<GemSO>();

        var mgr = ProgressionManager.TryGetRuntime();
        var pool = mgr != null && mgr.Catalog != null
            ? ProgressionLootFilter.FilterGems(table.allPossibleGems, mgr.Catalog, mgr)
            : table.allPossibleGems;

        return table.GetRandomGemsFromPool(count, pool);
    }

    public static List<RelicSO> RollRelics(RelicLootTableSO table, int count)
    {
        if (table == null)
            return new List<RelicSO>();

        var mgr = ProgressionManager.TryGetRuntime();
        var pool = mgr != null && mgr.Catalog != null
            ? ProgressionLootFilter.FilterRelics(table.allPossibleRelics, mgr.Catalog, mgr)
            : table.allPossibleRelics;

        return table.GetRandomRelicsFromPool(count, pool);
    }

    public static List<DieAssetSO> RollDice(
        DieLootTableSO table,
        int count,
        HashSet<DieType> preferredTypes,
        float preferredChance = 0.7f,
        bool uniqueInBatch = true)
    {
        if (table == null)
            return new List<DieAssetSO>();

        var mgr = ProgressionManager.TryGetRuntime();
        var pool = mgr != null && mgr.Catalog != null
            ? ProgressionLootFilter.FilterDice(table.allPossibleDice, mgr.Catalog, mgr)
            : table.allPossibleDice;

        return table.GetRandomDiceFromPool(count, pool, preferredTypes, preferredChance, uniqueInBatch);
    }
}
