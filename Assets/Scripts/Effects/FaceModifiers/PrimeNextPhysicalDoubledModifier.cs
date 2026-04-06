using System;
using UnityEngine;

/// <summary>After this face resolves, the next Physical damage face doubles its base damage once (Brute Force).</summary>
[Serializable]
public class PrimeNextPhysicalDoubledModifier : FaceResolveModifierBase
{
    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        registry.NextPhysicalDamageDoubled = true;
    }
}
