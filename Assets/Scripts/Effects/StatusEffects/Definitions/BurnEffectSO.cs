using UnityEngine;

[CreateAssetMenu(fileName = "Bleed", menuName = "DiceGame/StatusEffects/Bleed")]
public class BurnEffectSO : StatusEffectSO
{
    [SerializeField] private int damagePerStack = 2;

    public override void OnTurnStart(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        var damage = damagePerStack * instance.Stacks;
        if (target == StatusEffectTarget.Enemy)
            ctx.Enemy.TakeDamage(damage, EnemyDamagePresentationKind.Burn);
        else
            ctx.Player.TakeDamage(damage);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Burn] Turn-start tick: dealt {damage} burn damage to {target} ({damagePerStack}×{instance.Stacks} stacks)");
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
