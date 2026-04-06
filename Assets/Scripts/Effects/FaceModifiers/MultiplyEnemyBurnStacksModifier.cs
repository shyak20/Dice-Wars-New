using System;
using UnityEngine;

/// <summary>Multiplies enemy Burn stacks (Fanning Flames / Hellfire).</summary>
[Serializable]
public class MultiplyEnemyBurnStacksModifier : FaceResolveModifierBase
{
    [SerializeField] private int multiplier = 2;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (combat.activeEnemy == null || multiplier <= 1) return;
        combat.activeEnemy.StatusEffects.MultiplyStacks<BurnEffectSO>(multiplier, combat.BuildStatusContextForEffects());
    }
}
