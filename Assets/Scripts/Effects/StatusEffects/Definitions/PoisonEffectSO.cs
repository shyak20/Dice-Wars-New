using UnityEngine;

[CreateAssetMenu(fileName = "Poison", menuName = "DiceGame/StatusEffects/Poison")]
public class PoisonEffectSO : StatusEffectSO
{
    public override void OnBeforeEnemyTurn(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        var damage = instance.Stacks;
        ctx.Enemy.TakeTrueDamage(damage);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Poison] Dealt {damage} damage to enemy (ignores armor). Stacks: {instance.Stacks}");
    }
}
