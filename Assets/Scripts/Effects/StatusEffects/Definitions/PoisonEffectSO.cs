using UnityEngine;

/// <summary>
/// Each owner turn start: damage equal to current stacks. Enemy poison bypasses enemy armor via
/// <see cref="EnemyController.TakeTrueDamage"/>. Player poison bypasses armor via
/// <see cref="PlayerStatus.TakeTrueDamage"/>. Stack decay is <see cref="StatusEffectSO.stackDecayPerTurn"/>,
/// applied in <see cref="StatusEffectManager.TickTurnStart"/> after this runs.
/// Player Burn/Poison tick via <see cref="StatusEffectManager.TickTurnStartBeforePlayerArmorReset"/> at turn start
/// (Burn still consumes armor; Poison does not).
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
            ctx.Player.TakeTrueDamage(damage);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Poison] Turn-start tick: dealt {damage} true damage to {target}. Stacks before decay: {stacks}");
    }
}
