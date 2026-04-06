using System;
using UnityEngine;

/// <summary>Applies stacks of <see cref="PyromaniacEffectSO"/> to the player (buff asset).</summary>
[Serializable]
public class GrantPyromaniacModifier : FaceResolveModifierBase
{
    [SerializeField] private PyromaniacEffectSO pyromaniacDefinition;
    [SerializeField] private int stacks = 1;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (pyromaniacDefinition == null)
        {
            Debug.LogError("GrantPyromaniacModifier: assign pyromaniacDefinition (PyromaniacEffectSO asset).");
            return;
        }

        if (combat.player == null) return;

        var ctx = combat.BuildStatusContextForEffects();
        combat.player.StatusEffects.ApplyStatus(pyromaniacDefinition, Mathf.Max(1, stacks), ctx);
    }
}
