using System;
using UnityEngine;

/// <summary>
/// Deal X damage for every Y pip total across deck faces (value, damage, or armor), optionally filtered by element.
/// White Heat: metric = FaceValue, element = Fire, pipsPerStep = 1, damagePerStep = 1.
/// </summary>
[Serializable]
public class AddDamageFromDeckPipsModifier : FaceResolveModifierBase
{
    [SerializeField] private DeckPipMetric pipMetric = DeckPipMetric.FaceValue;

    [Tooltip("When off, only faces matching this element are counted.")]
    [SerializeField] private bool matchAnyElement = true;

    [SerializeField] private ElementType elementFilter = ElementType.Physical;

    [Tooltip("Deal this much damage per block of pips (e.g. 2 damage per 2 pips).")]
    [SerializeField, Min(1)] private int damagePerStep = 2;

    [Tooltip("Pip block size (e.g. every 2 pips).")]
    [SerializeField, Min(1)] private int pipsPerStep = 2;

    [Tooltip("When on, the face currently resolving is excluded from the deck sum.")]
    [SerializeField] private bool excludeTriggeringFace;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        var exclude = excludeTriggeringFace ? face : null;
        var totalPips = DeckFaceAggregation.SumDeckPips(pipMetric, elementFilter, matchAnyElement, exclude);
        var bonus = DeckFaceAggregation.ComputeSteppedBonus(totalPips, pipsPerStep, damagePerStep);
        if (bonus > 0)
            result.Damage += bonus;
    }
}
