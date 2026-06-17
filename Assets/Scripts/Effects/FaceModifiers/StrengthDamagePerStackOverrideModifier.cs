using System;
using UnityEngine;

/// <summary>
/// For this face only, each player <see cref="StrengthEffectSO"/> stack adds <see cref="damagePerStrength"/> to the attack
/// instead of the default 1 from Strength. Face pip damage and other per-die bonuses (non-Strength) are unchanged.
/// For damage that is only <c>X × Strength</c> with no base pip contribution, use <see cref="ScalingDamageFromStrengthModifier"/> instead.
/// </summary>
[Serializable]
public sealed class StrengthDamagePerStackOverrideModifier : FaceResolveModifierBase
{
    [Tooltip("Damage per Strength stack for this face. Strength normally adds 1 per stack; set 3 so each stack adds 3 instead of 1.")]
    [SerializeField, Min(0)] private int damagePerStrength = 2;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (combat.player == null) return;
        int str = combat.player.StatusEffects.GetStacks<StrengthEffectSO>();
        if (str <= 0) return;

        // CommitResolvedRoll already applied +str from Strength GetPerDieAttackDamageBonus (1 per stack).
        result.Damage += str * (damagePerStrength - 1);
    }
}
