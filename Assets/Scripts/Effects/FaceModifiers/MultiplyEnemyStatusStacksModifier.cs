using System;
using UnityEngine;

/// <summary>Multiplies stacks of a chosen enemy status (e.g. double Vulnerable).</summary>
[Serializable]
public class MultiplyEnemyStatusStacksModifier : FaceResolveModifierBase
{
    [SerializeField] private StatusEffectSO statusEffect;

    [SerializeField, Min(2)] private int stackMultiplier = 2;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (statusEffect == null)
        {
            Debug.LogError($"MultiplyEnemyStatusStacksModifier on face '{face?.name}': assign statusEffect.", combat);
            return;
        }

        if (combat?.activeEnemy == null || stackMultiplier < 2)
            return;

        var mgr = combat.activeEnemy.StatusEffects;
        if (mgr == null)
            return;

        mgr.MultiplyStacks(statusEffect, stackMultiplier, combat.BuildStatusContextForEffects());
    }
}
