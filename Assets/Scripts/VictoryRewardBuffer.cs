using System.Collections.Generic;

/// <summary>
/// Set by <see cref="CombatManager"/> on victory before <see cref="CombatEvents.OnPlayerVictory"/>.
/// Consumed by win-stage UI to show collectible rewards (gold/gems) before Continue.
/// </summary>
public static class VictoryRewardBuffer
{
    public static int PendingGold { get; set; }
    public static readonly List<GemSO> PendingGems = new List<GemSO>();

    public static void Clear()
    {
        PendingGold = 0;
        PendingGems.Clear();
    }
}
