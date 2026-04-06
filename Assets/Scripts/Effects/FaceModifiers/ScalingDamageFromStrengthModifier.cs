using System;
using UnityEngine;

/// <summary>Sets face damage to damagePerStrength × current Strength stacks (e.g. BulBul / Overpower style).</summary>
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
