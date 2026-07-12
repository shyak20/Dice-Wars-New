using System.Collections.Generic;

/// <summary>
/// Set by <see cref="CombatManager"/> on victory before <see cref="CombatEvents.OnPlayerVictory"/>.
/// Consumed by win-stage UI to show collectible rewards before Continue.
/// </summary>
public static class VictoryRewardBuffer
{
    public static int PendingGold { get; set; }
    public static readonly List<GemSO> PendingGems = new List<GemSO>();
    public static readonly List<RelicSO> PendingRelics = new List<RelicSO>();
    public static readonly List<DieAssetSO> PendingDice = new List<DieAssetSO>();

    public static void Clear()
    {
        PendingGold = 0;
        PendingGems.Clear();
        PendingRelics.Clear();
        PendingDice.Clear();
    }

    /// <summary>Queues a relic for win-stage collection only if the run does not already own it.</summary>
    public static bool TryAddPendingRelic(RelicSO relic)
    {
        if (relic == null || !RunRelicDraftFilter.IsAvailableForDraft(relic))
            return false;
        if (PendingRelics.Contains(relic))
            return false;
        PendingRelics.Add(relic);
        return true;
    }
}
