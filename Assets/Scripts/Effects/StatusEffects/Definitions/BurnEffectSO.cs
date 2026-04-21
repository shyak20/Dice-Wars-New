using UnityEngine;

/// <summary>
/// Each turn start: deals damage equal to current stacks (1 damage per stack). Stack removal is only from
/// <see cref="StatusEffectSO.stackDecayPerTurn"/>, applied in <see cref="StatusEffectManager.TickTurnStart"/> after this runs.
/// </summary>
[CreateAssetMenu(fileName = "Bleed", menuName = "DiceGame/StatusEffects/Bleed")]
public class BurnEffectSO : StatusEffectSO
{
    public override void OnTurnStart(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        int stacks = instance.Stacks;
        if (stacks <= 0) return;

        int damage = stacks;
        if (target == StatusEffectTarget.Enemy)
            ctx.Enemy.TakeDamage(damage, EnemyDamagePresentationKind.Burn);
        else
            ctx.Player.TakeDamage(damage);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Burn] Turn-start tick: dealt {damage} (1 per stack) on {target}; stacks before decay: {stacks}");
    }

    public override void OnPerfectStrike(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        if (target != StatusEffectTarget.Enemy) return;

        var damage = instance.Stacks;
        ctx.Enemy.TakeDamage(damage, EnemyDamagePresentationKind.Burn);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Burn] Perfect Strike! Dealt {damage} damage to enemy (stacks kept)");
    }
}
