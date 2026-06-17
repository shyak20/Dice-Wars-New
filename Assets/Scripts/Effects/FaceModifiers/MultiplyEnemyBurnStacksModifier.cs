using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Die face modifier: multiplies every <see cref="BurnEffectSO"/> on the enemy when this face resolves
/// (runs during gather per <see cref="FaceResolveModifierBase.ActivateImmediately"/>). Default 2 = double burn.
/// Add to <see cref="DieFaceSO.actions"/> like any other action.
/// </summary>
[Serializable]
public class MultiplyEnemyBurnStacksModifier : FaceResolveModifierBase
{
    [FormerlySerializedAs("multiplier")]
    [Tooltip("Each burn instance on the enemy becomes (current stacks) × this value. 2 = double, 3 = triple. No effect if below 2 or enemy has no burn.")]
    [SerializeField, Min(2)]
    private int stackMultiplier = 2;

    public int StackMultiplier => stackMultiplier;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (combat.activeEnemy == null || stackMultiplier < 2) return;
        var mgr = combat.activeEnemy.StatusEffects;
        if (mgr == null) return;
        mgr.MultiplyAllBurnStacks(stackMultiplier, combat.BuildStatusContextForEffects());
    }
}
