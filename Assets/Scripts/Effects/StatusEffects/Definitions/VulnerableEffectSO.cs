using UnityEngine;

/// <summary>
/// Enemy debuff: each stack amplifies damage the enemy takes from the player turn physical attack total
/// (<see cref="StatusEffectManager.ApplyDamageModifiers"/> in <see cref="CombatManager.SubmitTurn"/>).
/// Burn ticks, thorns, supernova bust, and other direct <see cref="EnemyController.TakeDamage"/> calls do not use this hook.
/// </summary>
[CreateAssetMenu(fileName = "Vulnerable", menuName = "DiceGame/StatusEffects/Vulnerable")]
public sealed class VulnerableEffectSO : StatusEffectSO
{
    [Tooltip("Additional damage per stack as a percent of incoming attack (10 = +10% per stack, multiplicative with other ModifyDamageToOwner effects).")]
    [SerializeField, Range(0, 200)] private int extraDamagePercentPerStack = 10;

    private void OnValidate()
    {
        if (target != StatusEffectTarget.Enemy)
            Debug.LogError($"{nameof(VulnerableEffectSO)} ({name}): Target must be Enemy — this modifies damage taken by the debuffed enemy.");
    }

    public override int ModifyDamageToOwner(StatusEffectInstance instance, StatusEffectContext ctx, int damage)
    {
        if (damage <= 0 || instance.Stacks <= 0 || extraDamagePercentPerStack <= 0)
            return damage;

        var mult = 1f + extraDamagePercentPerStack / 100f * instance.Stacks;
        var modified = Mathf.RoundToInt(damage * mult);

        if (GameActionDebug.Enabled)
            Debug.Log($"[Vulnerable] Player physical attack vs enemy: {damage} → {modified} ({instance.Stacks} stacks, ×{mult:F2})");

        return modified;
    }
}
