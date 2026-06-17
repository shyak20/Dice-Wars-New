using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Enemy debuff: while stacks &gt; 0, increases damage taken from <see cref="EnemyDamagePresentationKind.Burn"/>
/// by a flat percent (not scaled by stack count). Each turn, stacks decay by <see cref="StatusEffectSO.stackDecayPerTurn"/>.
/// </summary>
[CreateAssetMenu(fileName = "Ablaze", menuName = "DiceGame/StatusEffects/Ablaze")]
public sealed class AblazeEffectSO : StatusEffectSO
{
    [FormerlySerializedAs("extraFireDamagePercentPerStack")]
    [Tooltip("Additional damage taken from burn while this effect has stacks (10 = +10% total, same at 1 stack or many).")]
    [SerializeField, Range(0, 200)] private int extraFireDamagePercent = 10;

    private void OnValidate()
    {
        if (target != StatusEffectTarget.Enemy)
            Debug.LogError($"{nameof(AblazeEffectSO)} ({name}): Target must be Enemy — this modifies burn damage taken by the debuffed enemy.");
    }

    public override int ModifyBurnDamageToOwner(StatusEffectInstance instance, StatusEffectContext ctx, int damage)
    {
        if (damage <= 0 || instance.Stacks <= 0 || extraFireDamagePercent <= 0)
            return damage;

        var mult = 1f + extraFireDamagePercent / 100f;
        var modified = Mathf.RoundToInt(damage * mult);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Ablaze] Burn damage vs enemy: {damage} → {modified} (×{mult:F2}, stacks {instance.Stacks})");

        return modified;
    }
}
