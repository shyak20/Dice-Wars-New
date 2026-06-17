using System;
using UnityEngine;

/// <summary>Adds TurnRegistry accumulated armor (this turn, from earlier rolls) to this face's damage.</summary>
[Serializable]
public class AddDamageFromAccumulatedArmorModifier : FaceResolveModifierBase
{
    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        result.Damage += Mathf.Max(0, registry.AccumulatedArmor);
    }
}
