using UnityEngine;

/// <summary>
/// Each owner turn start: damage equal to current stacks (ignores armor on enemies). Stack decay is
/// <see cref="StatusEffectSO.stackDecayPerTurn"/>, applied in <see cref="StatusEffectManager.TickTurnStart"/> after this runs.
/// Uses <see cref="OnTurnStart"/> (not <see cref="OnBeforeEnemyTurn"/>) so debuffs on the player actually tick:
/// <see cref="StatusEffectManager.TickBeforeEnemyTurn"/> only runs on the enemy's status manager.
/// </summary>
[CreateAssetMenu(fileName = "Poison", menuName = "DiceGame/StatusEffects/Poison")]
public class PoisonEffectSO : StatusEffectSO
{
    public override void OnTurnStart(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        var stacks = instance.Stacks;
        if (stacks <= 0) return;

        var damage = stacks;
        if (target == StatusEffectTarget.Enemy)
            ctx.Enemy.TakeTrueDamage(damage);
        else
            ctx.Player.TakeDamage(damage, PlayerDamageSource.Generic);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Poison] Turn-start tick: dealt {damage} to {target} (enemy = true damage). Stacks before decay: {stacks}");
    }
}
