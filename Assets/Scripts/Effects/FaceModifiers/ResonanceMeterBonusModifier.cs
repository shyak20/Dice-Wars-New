using System;
using UnityEngine;

/// <summary>
/// Before this die's value is added to the Resonance Meter, grants a bonus equal to <see cref="CombatManager.GetCurrentPower"/>.
/// Armor/damage apply to this face's result; fire/poison apply stacks to the enemy and show in the stored-actions pool.
/// </summary>
[Serializable]
public class ResonanceMeterBonusModifier : FaceResolveModifierBase
{
    [SerializeField] private CombatBonusChannel bonusChannel = CombatBonusChannel.Armor;

    [Tooltip("Required when Bonus Channel is Fire.")]
    [SerializeField] private BurnEffectSO burnDefinition;

    [Tooltip("Required when Bonus Channel is Poison.")]
    [SerializeField] private PoisonEffectSO poisonDefinition;

    public CombatBonusChannel BonusChannel => bonusChannel;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        if (combat == null || result == null)
            return;

        var resonance = combat.GetCurrentPower();
        if (resonance <= 0)
            return;

        CombatBonusChannelApplicator.ApplyToFaceResult(
            bonusChannel,
            resonance,
            result,
            combat,
            burnDefinition,
            poisonDefinition);

        if (GameActionDebug.Enabled)
            Debug.Log($"[ResonanceMeterBonus] +{resonance} {bonusChannel} (meter before this roll).");
    }
}
