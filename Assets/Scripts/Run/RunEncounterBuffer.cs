/// <summary>Cross-scene handoff for map-driven combat (cleared after <see cref="CombatManager"/> consumes it).</summary>
public static class RunEncounterBuffer
{
    public static EnemyTypeSO PendingEnemyType { get; private set; }
    public static bool PendingCombatWasBoss { get; private set; }

    public static void SetPendingCombat(EnemyTypeSO enemy, bool isBossTile)
    {
        PendingEnemyType = enemy;
        PendingCombatWasBoss = isBossTile;
    }

    public static bool TryConsumePendingEnemy(out EnemyTypeSO enemy)
    {
        enemy = PendingEnemyType;
        PendingEnemyType = null;
        return enemy != null;
    }

    /// <summary>Call after victory continue when returning to the map; clears the boss flag.</summary>
    public static bool TakeAndClearWasBossFight()
    {
        var b = PendingCombatWasBoss;
        PendingCombatWasBoss = false;
        return b;
    }

    public static void AbortPendingMapCombatState()
    {
        PendingEnemyType = null;
        PendingCombatWasBoss = false;
    }
}
