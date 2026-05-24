using System;
using UnityEngine;

/// <summary>
/// Adds damage from enemy status stacks. Leave <see cref="statusEffect"/> unset to sum every active effect;
/// assign a definition (e.g. Burn) to only count matching stacks.
/// </summary>
[Serializable]
public class AddDamageFromEnemyStatusStacksModifier : FaceResolveModifierBase
{
    public enum EnemyStatusCountMode
    {
        /// <summary>Sum stacks on each matching instance (Flaming Sword: 1 damage per burn stack).</summary>
        SumMatchingStacks,
        /// <summary>+<see cref="damagePerUnit"/> once per active status instance (Damage per Status).</summary>
        CountActiveEffects,
    }

    [Tooltip("Optional. When set, only this status definition is counted; when unset, all enemy effects with stacks are included.")]
    [SerializeField] private StatusEffectSO statusEffect;

    [SerializeField] private EnemyStatusCountMode countMode = EnemyStatusCountMode.SumMatchingStacks;

    [Tooltip("Damage per stack (SumMatchingStacks) or per active effect (CountActiveEffects).")]
    [SerializeField, Min(0)] private int damagePerUnit = 1;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (combat?.activeEnemy == null || damagePerUnit <= 0)
            return;

        var mgr = combat.activeEnemy.StatusEffects;
        if (mgr == null)
            return;

        var amount = countMode == EnemyStatusCountMode.CountActiveEffects
            ? CountActiveEffects(mgr) * damagePerUnit
            : SumMatchingStacks(mgr) * damagePerUnit;

        if (amount > 0)
            result.Damage += amount;
    }

    int SumMatchingStacks(StatusEffectManager mgr)
    {
        var effects = mgr.Effects;
        if (effects == null)
            return 0;

        var sum = 0;
        for (var i = 0; i < effects.Count; i++)
        {
            var inst = effects[i];
            if (inst == null || inst.Stacks <= 0)
                continue;
            if (statusEffect != null && inst.Definition != statusEffect)
                continue;
            sum += inst.Stacks;
        }

        return sum;
    }

    int CountActiveEffects(StatusEffectManager mgr)
    {
        var effects = mgr.Effects;
        if (effects == null)
            return 0;

        var count = 0;
        for (var i = 0; i < effects.Count; i++)
        {
            var inst = effects[i];
            if (inst == null || inst.Stacks <= 0)
                continue;
            if (statusEffect != null && inst.Definition != statusEffect)
                continue;
            count++;
        }

        return count;
    }
}
