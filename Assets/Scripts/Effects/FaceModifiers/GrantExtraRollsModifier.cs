using System;
using UnityEngine;

/// <summary>Adds extra rolls this player turn (e.g. Moshe).</summary>
[Serializable]
public class GrantExtraRollsModifier : FaceResolveModifierBase
{
    [SerializeField] private int extraRolls = 1;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        combat.AddRollsRemaining(Mathf.Max(0, extraRolls));
    }
}
