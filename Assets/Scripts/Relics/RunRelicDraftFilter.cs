using System.Collections.Generic;

/// <summary>
/// Relic loot eligibility for the current run. Only relics already in
/// <see cref="RunManager.RunRelics"/> are excluded — declining or skipping an offer does not blacklist it.
/// Unowned relics remain eligible on every shop restock, treasure roll, and fight bonus until acquired.
/// </summary>
public static class RunRelicDraftFilter
{
    public static bool IsAvailableForDraft(RelicSO relic)
    {
        if (relic == null)
            return false;

        var run = RunManager.Instance;
        return run == null || !run.HasRunRelic(relic);
    }

    public static List<RelicSO> ExcludeRunOwned(IReadOnlyList<RelicSO> pool)
    {
        if (pool == null || pool.Count == 0)
            return new List<RelicSO>();

        var run = RunManager.Instance;
        if (run == null)
            return CopyNonNull(pool);

        var result = new List<RelicSO>();
        for (var i = 0; i < pool.Count; i++)
        {
            var relic = pool[i];
            if (relic == null || run.HasRunRelic(relic))
                continue;
            result.Add(relic);
        }

        return result;
    }

    static List<RelicSO> CopyNonNull(IReadOnlyList<RelicSO> pool)
    {
        var list = new List<RelicSO>();
        for (var i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null)
                list.Add(pool[i]);
        }

        return list;
    }
}
