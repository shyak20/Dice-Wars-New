using System;
using UnityEngine;

/// <summary>How to read each face on dice in the run deck when summing pips.</summary>
public enum DeckPipMetric
{
    FaceValue,
    FaceDamage,
    FaceArmor,
}

/// <summary>Shared helpers for face modifiers that sum values across <see cref="PlayerDataContainer"/> deck faces.</summary>
public static class DeckFaceAggregation
{
    public static int SumDeckPips(
        DeckPipMetric metric,
        ElementType elementFilter,
        bool matchAnyElement,
        DieFaceSO excludeFace)
    {
        var container = PlayerDataContainer.Instance;
        if (container?.RuntimeData?.currentDeck == null)
            return 0;

        var deck = container.RuntimeData.currentDeck;
        var total = 0;
        for (var d = 0; d < deck.Count; d++)
        {
            var die = deck[d];
            if (die?.faces == null)
                continue;

            for (var i = 0; i < die.faces.Length; i++)
            {
                var face = die.faces[i];
                if (face == null || face == excludeFace)
                    continue;

                if (!matchAnyElement && face.Element != elementFilter)
                    continue;

                total += GetMetricValue(face, metric);
            }
        }

        return Mathf.Max(0, total);
    }

    public static int ComputeSteppedBonus(int totalPips, int pipsPerStep, int damagePerStep)
    {
        if (pipsPerStep <= 0 || damagePerStep <= 0 || totalPips <= 0)
            return 0;

        return totalPips / pipsPerStep * damagePerStep;
    }

    static int GetMetricValue(DieFaceSO face, DeckPipMetric metric)
    {
        switch (metric)
        {
            case DeckPipMetric.FaceDamage:
                return Mathf.Max(0, face.damage);
            case DeckPipMetric.FaceArmor:
                return Mathf.Max(0, face.armor);
            case DeckPipMetric.FaceValue:
            default:
                return Mathf.Max(0, face.value);
        }
    }
}
