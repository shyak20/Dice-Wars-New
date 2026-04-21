using System;
using UnityEngine;

/// <summary>Sets face damage to damagePerStrength × current Strength stacks only (replaces base pip damage and the usual +1/stack Strength bonus). To keep face damage and only change 1/stack → X/stack, use <see cref="StrengthDamagePerStackOverrideModifier"/>.</summary>
[Serializable]
public class ScalingDamageFromStrengthModifier : FaceResolveModifierBase
{
    [SerializeField] private int damagePerStrength = 5;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (combat.player == null) return;
        var str = combat.player.StatusEffects.GetStacks<StrengthEffectSO>();
        result.Damage = damagePerStrength * Mathf.Max(0, str);
    }
}
