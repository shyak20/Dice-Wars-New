/// <summary>
/// Set by <see cref="CombatManager"/> on victory before <see cref="CombatEvents.OnPlayerVictory"/>.
/// Consumed by win-stage UI to show collectible gold (not granted until the player collects it).
/// </summary>
public static class VictoryRewardBuffer
{
    public static int PendingGold { get; set; }

    public static void Clear() => PendingGold = 0;
}
