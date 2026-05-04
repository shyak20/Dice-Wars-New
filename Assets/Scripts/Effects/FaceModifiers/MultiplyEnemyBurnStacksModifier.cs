using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Die face modifier: multiplies the enemy’s current <see cref="BurnEffectSO"/> stacks when this face resolves
/// (runs during roll gather, before the face is recorded on the turn). Add to <see cref="DieFaceSO.actions"/> like any other action.
/// </summary>
[Serializable]
public class MultiplyEnemyBurnStacksModifier : FaceResolveModifierBase
{
    [FormerlySerializedAs("multiplier")]
    [Tooltip("Enemy burn stacks become (current stacks) × this value. Examples: 2 = double, 3 = triple. No effect if below 2 or enemy has no burn.")]
    [SerializeField, Min(2)]
    private int stackMultiplier = 2;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (combat.activeEnemy == null || stackMultiplier < 2) return;
        combat.activeEnemy.StatusEffects.MultiplyStacks<BurnEffectSO>(stackMultiplier, combat.BuildStatusContextForEffects());
    }
}
