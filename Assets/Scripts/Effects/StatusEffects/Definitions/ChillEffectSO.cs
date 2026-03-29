using UnityEngine;

[CreateAssetMenu(fileName = "Chill", menuName = "DiceGame/StatusEffects/Chill")]
public class ChillEffectSO : StatusEffectSO
{
    [SerializeField] private int frozenThreshold = 5;
    [SerializeField] private StatusEffectSO frozenEffect;

    private void OnValidate()
    {
        if (frozenEffect != null && frozenEffect is not FrozenEffectSO)
            Debug.LogError($"ChillEffectSO: frozenEffect must be a FrozenEffectSO, got {frozenEffect.GetType().Name}");
    }

    public override void OnApply(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        if (frozenEffect == null)
        {
            Debug.LogError("ChillEffectSO: frozenEffect reference is not assigned!");
            return;
        }

        if (instance.Stacks >= frozenThreshold)
        {
            var enemyEffects = ctx.Enemy.StatusEffects;
            enemyEffects.ApplyStatus(frozenEffect, 1, ctx);

            if (GameActionDebug.Enabled)
                Debug.Log($"[Chill] Reached {instance.Stacks} stacks (threshold: {frozenThreshold}), applied Frozen!");
        }
    }

    public override int ModifyEnemyHitDamage(StatusEffectInstance instance, StatusEffectContext ctx, int damage)
    {
        var reduced = Mathf.Max(0, damage - instance.Stacks);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Chill] Reduced enemy hit from {damage} to {reduced} ({instance.Stacks} stacks)");

        return reduced;
    }
}
