using UnityEngine;

[CreateAssetMenu(fileName = "Bleed", menuName = "DiceGame/StatusEffects/Bleed")]
public class BurnEffectSO : StatusEffectSO
{
    [SerializeField] private int damagePerStack = 2;

    public override void OnAfterEnemyTurn(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        var damage = damagePerStack * instance.Stacks;
        ctx.Enemy.TakeDamage(damage);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Burn] Dealt {damage} damage to enemy ({damagePerStack}×{instance.Stacks} stacks)");
    }

    public override void OnPerfectStrike(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        var damage = instance.Stacks;
        ctx.Enemy.TakeDamage(damage);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Burn] Perfect Strike! Dealt {damage} damage to enemy (stacks kept)");
    }
}
