using UnityEngine;

[CreateAssetMenu(fileName = "Shattered", menuName = "DiceGame/StatusEffects/Shattered")]
public class ShatteredEffectSO : StatusEffectSO
{
    [SerializeField, Range(1, 100)] private int damageReductionPercent = 25;

    public override int ModifyEnemyHitDamage(StatusEffectInstance instance, StatusEffectContext ctx, int damage)
    {
        var reduced = Mathf.RoundToInt(damage * (1f - damageReductionPercent / 100f));
        reduced = Mathf.Max(0, reduced);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Shattered] Reduced enemy hit from {damage} to {reduced} (-{damageReductionPercent}%, {instance.Stacks} turns remaining)");

        return reduced;
    }
}
