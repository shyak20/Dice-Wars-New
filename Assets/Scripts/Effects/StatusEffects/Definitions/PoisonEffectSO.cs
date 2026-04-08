using UnityEngine;

[CreateAssetMenu(fileName = "Poison", menuName = "DiceGame/StatusEffects/Poison")]
public class PoisonEffectSO : StatusEffectSO
{
    public override bool TryGetRollFlyoutContribution(int displayedStacks, StatusEffectTarget applyTarget, out DieType poolType, out int poolAmount)
    {
        poolType = DieType.Nature;
        poolAmount = displayedStacks;
        return applyTarget == StatusEffectTarget.Enemy && displayedStacks > 0;
    }

    public override void OnBeforeEnemyTurn(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        var damage = instance.Stacks;
        ctx.Enemy.TakeTrueDamage(damage);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Poison] Dealt {damage} damage to enemy (ignores armor). Stacks: {instance.Stacks}");
    }
}
