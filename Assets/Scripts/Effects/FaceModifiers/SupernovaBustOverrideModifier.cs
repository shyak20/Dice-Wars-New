using System;
using UnityEngine;

/// <summary>If you bust, skip bust UI and deal extra damage; uses face value as damage unless override is set.</summary>
[Serializable]
public class SupernovaBustOverrideModifier : FaceResolveModifierBase
{
    [Tooltip("If negative, uses face.value as bust bonus damage.")]
    [SerializeField] private int bustBonusDamage = -1;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        registry.SupernovaBustOverrideActive = true;
        registry.SupernovaBustDamage = bustBonusDamage >= 0 ? bustBonusDamage : Mathf.Max(0, face.value);
    }
}
