using System;
using UnityEngine;

/// <summary>Adds this face's damage equal to the enemy's current armor.</summary>
[Serializable]
public class AddDamageFromEnemyArmorModifier : FaceResolveModifierBase
{
    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (combat?.activeEnemy == null)
            return;

        var armor = combat.activeEnemy.GetCurrentArmor();
        if (armor > 0)
            result.Damage += armor;
    }
}
