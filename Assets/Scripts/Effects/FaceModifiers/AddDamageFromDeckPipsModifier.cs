using System;
using UnityEngine;

/// <summary>
/// Deal damage from deck faces filtered by element and optional metric value.
/// Match mode (metric value 1–6): each equipped face with that pip/damage/armor counts once × damage per match.
/// Sum mode (metric value 0): add every filtered face's metric total × damage per match (White Heat).
/// </summary>
[Serializable]
public class AddDamageFromDeckPipsModifier : FaceResolveModifierBase, IFaceDescriptionPreviewValue
{
    [SerializeField] private DeckPipMetric pipMetric = DeckPipMetric.FaceValue;

    [Tooltip("When off, only faces matching this element are counted.")]
    [SerializeField] private bool matchAnyElement = true;

    [SerializeField] private ElementType elementFilter = ElementType.Physical;

    [Tooltip("Which pip to count on deck faces (uses Pip Metric). 0 = sum all filtered faces instead of matching one value.")]
    [SerializeField, Range(0, 6)] private int matchMetricValue = 1;

    [Tooltip("Damage dealt per matching face (match mode) or multiplied against the summed metric (sum mode).")]
    [SerializeField, Min(1)] private int damagePerMatch = 1;

    [Tooltip("When on, the face currently resolving is excluded from the deck count.")]
    [SerializeField] private bool excludeTriggeringFace;

    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        var bonus = ComputeBonusDamage(face);
        if (bonus > 0)
            result.Damage += bonus;
    }

    public bool TryGetDescriptionPreviewValue(DieFaceSO face, out int value)
    {
        value = ComputeBonusDamage(face);
        return true;
    }

    int ComputeBonusDamage(DieFaceSO face)
    {
        var exclude = excludeTriggeringFace ? face : null;
        var countMatching = matchMetricValue >= 1;
        var total = DeckFaceAggregation.SumDeckPips(
            pipMetric,
            elementFilter,
            matchAnyElement,
            exclude,
            countMatching ? matchMetricValue : (int?)null,
            countEachMatchingFaceAsOne: countMatching);
        return total * damagePerMatch;
    }
}
