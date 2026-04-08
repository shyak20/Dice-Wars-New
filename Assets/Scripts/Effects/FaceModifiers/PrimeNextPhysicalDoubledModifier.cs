using System;
using UnityEngine;

/// <summary>
/// Queues a one-shot multiplier for the next eligible roll this turn.
/// Supports damage-only, armor-only, or both channels.
/// </summary>
[Serializable]
public class PrimeNextPhysicalDoubledModifier : FaceResolveModifierBase
{
    [SerializeField] private bool multiplyDamage = true;
    [SerializeField] private bool multiplyArmor = false;
    [SerializeField, Min(0f)] private float multiplier = 2f;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (!multiplyDamage && !multiplyArmor)
        {
            Debug.LogError("PrimeNextPhysicalDoubledModifier: enable multiplyDamage and/or multiplyArmor.");
            return;
        }

        registry.NextRollMultiplierActive = true;
        registry.NextRollMultiplyDamage = multiplyDamage;
        registry.NextRollMultiplyArmor = multiplyArmor;
        registry.NextRollMultiplier = multiplier;
    }
}
