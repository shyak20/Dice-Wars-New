using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Shattered", menuName = "DiceGame/StatusEffects/Shattered")]
public class ShatteredEffectSO : StatusEffectSO
{
    [Header("Enemy Attack Power Reduction")]
    [Tooltip("Percent attack-power reduction applied per stack of Shattered.")]
    [FormerlySerializedAs("damageReductionPercent")]
    [SerializeField, Range(0f, 100f)] private float reductionPerStackPercent = 25f;
    [Tooltip("Maximum total attack-power reduction from Shattered stacks.")]
    [SerializeField, Range(0f, 100f)] private float maxReductionPercent = 75f;

    public override int ModifyEnemyHitDamage(StatusEffectInstance instance, StatusEffectContext ctx, int damage)
    {
        var stacks = Mathf.Max(0, instance != null ? instance.Stacks : 0);
        var totalReductionPercent = Mathf.Clamp(stacks * reductionPerStackPercent, 0f, maxReductionPercent);
        var reduced = Mathf.RoundToInt(damage * (1f - totalReductionPercent / 100f));
        reduced = Mathf.Max(0, reduced);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Shattered] Reduced enemy hit from {damage} to {reduced} (-{totalReductionPercent:0.##}%, {stacks} stack(s), cap {maxReductionPercent:0.##}%)");

        return reduced;
    }
}
