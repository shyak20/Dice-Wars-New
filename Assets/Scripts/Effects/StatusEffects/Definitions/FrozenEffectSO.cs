using UnityEngine;

[CreateAssetMenu(fileName = "Frozen", menuName = "DiceGame/StatusEffects/Frozen")]
public class FrozenEffectSO : StatusEffectSO
{
    [SerializeField] private float damageIncreasePerStack = 0.2f;

    public override bool TryGetRollFlyoutContribution(int displayedStacks, StatusEffectTarget applyTarget, out DieType poolType, out int poolAmount)
    {
        poolType = DieType.Ice;
        poolAmount = displayedStacks;
        return applyTarget == StatusEffectTarget.Enemy && displayedStacks > 0;
    }

    public override int ModifyDamageToOwner(StatusEffectInstance instance, StatusEffectContext ctx, int damage)
    {
        var multiplier = 1f + damageIncreasePerStack * instance.Stacks;
        var modified = Mathf.RoundToInt(damage * multiplier);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Frozen] Damage to enemy: {damage} → {modified} ({instance.Stacks} stacks, ×{multiplier:F1})");

        return modified;
    }
}
