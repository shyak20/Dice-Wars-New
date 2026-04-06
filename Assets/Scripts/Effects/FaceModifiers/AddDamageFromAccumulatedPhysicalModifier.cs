using System;
using UnityEngine;

/// <summary>Adds multiplier × accumulated physical damage from earlier rolls this turn (Double/Triple Strike style).</summary>
[Serializable]
public class AddDamageFromAccumulatedPhysicalModifier : FaceResolveModifierBase
{
    [SerializeField] private int physicalDamageMultiplier = 2;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        result.Damage += Mathf.Max(0, physicalDamageMultiplier) * Mathf.Max(0, registry.AccumulatedPhysicalDamage);
    }
}
