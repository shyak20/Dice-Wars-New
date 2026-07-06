using System;
using UnityEngine;

/// <summary>Deferred: damages the player when the turn is submitted (before enemy physical damage).</summary>
[Serializable]
public class DealPlayerDamageOnSubmitAction : GameActionWithIcon
{
    [SerializeField, Min(0)] private int damage = 1;

    public int Damage => damage;

    protected override ActionVisualId VisualKey => ActionVisualId.DealPlayerDamageOnSubmit;

    /// <summary>Accumulates into <see cref="FaceResult.SelfDamage"/> when the face has no <see cref="DieFaceSO.selfDamage"/> value.</summary>
    public void AppendPoolContributionIfAny(FaceResult result)
    {
        if (ActivateImmediately || result == null || damage <= 0 || result.SelfDamage > 0)
            return;

        result.SelfDamage = damage;
    }

    public override void Execute(GameActionContext context)
    {
        // Applied once on submit via CombatManager.ApplyCurseSelfDamageFromChanneledFaces from FaceResult.SelfDamage.
        if (GameActionDebug.Enabled && damage > 0)
            Debug.Log($"[DealPlayerDamageOnSubmit] {damage} self damage resolved via FaceResult.SelfDamage on submit.");
    }
}
