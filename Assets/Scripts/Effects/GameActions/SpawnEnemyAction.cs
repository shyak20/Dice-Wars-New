using System;
using UnityEngine;

/// <summary>
/// Enemy intent action: spawns an additional enemy into a free roster slot mid-combat (up to the multi-enemy cap).
/// Use on an <see cref="EnemyActionSO"/> so a Main Enemy can summon adds during its turn. No-op when the roster is full.
/// </summary>
[Serializable]
public class SpawnEnemyAction : IGameAction
{
    [Tooltip("Enemy type to spawn into a free slot. Should itself have no spawn-on-load adds (adds do not summon adds).")]
    [SerializeField] private EnemyTypeSO enemyToSpawn;

    public EnemyTypeSO EnemyToSpawn => enemyToSpawn;

    public void Execute(GameActionContext context)
    {
        if (context == null || context.CombatManager == null)
            return;

        if (enemyToSpawn == null)
        {
            Debug.LogError("SpawnEnemyAction: enemyToSpawn is not assigned.");
            return;
        }

        var spawned = context.CombatManager.SpawnEnemy(enemyToSpawn);
        if (GameActionDebug.Enabled)
        {
            if (spawned != null)
                Debug.Log($"[SpawnEnemyAction] Spawned '{enemyToSpawn.enemyName}' into the roster.");
            else
                Debug.Log($"[SpawnEnemyAction] Could not spawn '{enemyToSpawn.enemyName}' — roster is full or no free slot.");
        }
    }
}
