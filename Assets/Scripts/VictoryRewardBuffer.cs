using System.Collections.Generic;

/// <summary>
/// Set by <see cref="CombatManager"/> on victory before <see cref="CombatEvents.OnPlayerVictory"/>.
/// Consumed by win-stage UI to show collectible rewards before Continue.
/// </summary>
public static class VictoryRewardBuffer
{
    public static int PendingGold { get; set; }
    public static int PendingRubyShards { get; set; }
    public static readonly List<GemSO> PendingGems = new List<GemSO>();
    public static readonly List<RelicSO> PendingRelics = new List<RelicSO>();
    public static readonly List<DieAssetSO> PendingDice = new List<DieAssetSO>();

    public static void Clear()
    {
        PendingGold = 0;
        PendingRubyShards = 0;
        PendingGems.Clear();
        PendingRelics.Clear();
        PendingDice.Clear();
    }
}
