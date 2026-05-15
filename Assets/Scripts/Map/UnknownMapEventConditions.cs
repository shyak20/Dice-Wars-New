using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Polymorphic predicate for unknown map event visibility and per-option enablement (AND within a list).
/// Assign concrete types via SerializeReference like die-face actions.
/// </summary>
[Serializable]
public abstract class UnknownMapEventConditionBase
{
    public abstract bool Evaluate(UnknownMapEventEvaluationContext ctx);
}

[Serializable]
public sealed class UnknownMapEventConditionAlwaysTrue : UnknownMapEventConditionBase
{
    public override bool Evaluate(UnknownMapEventEvaluationContext ctx) => true;
}

[Serializable]
public sealed class UnknownMapEventConditionDeckDiceCountMin : UnknownMapEventConditionBase
{
    [Min(0)]
    [Tooltip("Requires at least this many non-null dice in the run deck.")]
    public int minimumCount;

    public override bool Evaluate(UnknownMapEventEvaluationContext ctx) =>
        UnknownMapEventConditionShared.CountDeckDice(ctx) >= Mathf.Max(0, minimumCount);
}

[Serializable]
public sealed class UnknownMapEventConditionGoldMin : UnknownMapEventConditionBase
{
    [Tooltip("Requires at least this much run gold.")]
    public int minimumGold;

    public override bool Evaluate(UnknownMapEventEvaluationContext ctx) =>
        UnknownMapEventConditionShared.GetGold(ctx) >= minimumGold;
}

[Serializable]
public sealed class UnknownMapEventConditionRunCurrentHpMin : UnknownMapEventConditionBase
{
    [Tooltip("Requires current run HP at least this value.")]
    public int minimumHp;

    public override bool Evaluate(UnknownMapEventEvaluationContext ctx) =>
        UnknownMapEventConditionShared.GetRunCurrentHp(ctx) >= minimumHp;
}

[Serializable]
public sealed class UnknownMapEventConditionRunMaxHpMin : UnknownMapEventConditionBase
{
    [Tooltip("Requires run max HP at least this value.")]
    public int minimumMaxHp;

    public override bool Evaluate(UnknownMapEventEvaluationContext ctx) =>
        UnknownMapEventConditionShared.GetRunMaxHp(ctx) >= minimumMaxHp;
}

[Serializable]
public sealed class UnknownMapEventConditionHasDieOfType : UnknownMapEventConditionBase
{
    [Tooltip("Requires at least one die in the deck with this type.")]
    public DieType dieType;

    public override bool Evaluate(UnknownMapEventEvaluationContext ctx) =>
        UnknownMapEventConditionShared.HasDieOfType(ctx, dieType);
}

[Serializable]
public sealed class UnknownMapEventConditionHasAnyCurseFaceOnDeck : UnknownMapEventConditionBase
{
    public override bool Evaluate(UnknownMapEventEvaluationContext ctx) =>
        UnknownMapEventConditionShared.HasAnyCurseFace(ctx);
}

[Serializable]
public sealed class UnknownMapEventConditionDeckTotalSocketedGemsMin : UnknownMapEventConditionBase
{
    [Min(0)]
    [Tooltip("Requires at least this many non-null gems socketed across the whole deck.")]
    public int minimumGems;

    public override bool Evaluate(UnknownMapEventEvaluationContext ctx) =>
        UnknownMapEventConditionShared.CountSocketedGems(ctx) >= Mathf.Max(0, minimumGems);
}

/// <summary>
/// Requires the linked unknown event to have been completed this run (Clear Event chains).
/// </summary>
[Serializable]
public sealed class UnknownMapEventConditionCompletedUnknownEvent : UnknownMapEventConditionBase
{
    [Tooltip("Unknown map event asset that must be completed this run (uses its ResolvedEventId).")]
    public UnknownMapEventSO requiredCompletedUnknownEvent;

    public override bool Evaluate(UnknownMapEventEvaluationContext ctx)
    {
        if (requiredCompletedUnknownEvent == null)
        {
            Debug.LogError(
                "UnknownMapEventConditionCompletedUnknownEvent: requiredCompletedUnknownEvent is not assigned.");
            return false;
        }

        if (ctx.RunManager == null)
            return false;
        return ctx.RunManager.IsUnknownMapEventCompleted(requiredCompletedUnknownEvent.ResolvedEventId);
    }
}

/// <summary>Shared helpers for condition evaluation.</summary>
public static class UnknownMapEventConditionShared
{
    public static int CountDeckDice(UnknownMapEventEvaluationContext ctx)
    {
        var data = PlayerDataContainer.Instance?.RuntimeData;
        if (data?.currentDeck == null)
            return 0;
        var n = 0;
        for (var i = 0; i < data.currentDeck.Count; i++)
        {
            if (data.currentDeck[i] != null)
                n++;
        }

        return n;
    }

    public static int GetGold(UnknownMapEventEvaluationContext ctx)
    {
        var eco = RunEconomyManager.Instance;
        return eco != null ? eco.CurrentGold : 0;
    }

    public static int GetRunCurrentHp(UnknownMapEventEvaluationContext ctx)
    {
        if (ctx.RunManager == null)
            return 0;
        return ctx.RunManager.RunCurrentHp;
    }

    public static int GetRunMaxHp(UnknownMapEventEvaluationContext ctx)
    {
        if (ctx.RunManager == null)
            return 0;
        return ctx.RunManager.RunMaxHp;
    }

    public static bool HasDieOfType(UnknownMapEventEvaluationContext ctx, DieType type)
    {
        var data = PlayerDataContainer.Instance?.RuntimeData;
        if (data?.currentDeck == null)
            return false;
        for (var i = 0; i < data.currentDeck.Count; i++)
        {
            var d = data.currentDeck[i];
            if (d != null && d.dieType == type)
                return true;
        }

        return false;
    }

    public static bool HasAnyCurseFace(UnknownMapEventEvaluationContext ctx)
    {
        var data = PlayerDataContainer.Instance?.RuntimeData;
        if (data?.currentDeck == null)
            return false;
        for (var d = 0; d < data.currentDeck.Count; d++)
        {
            var die = data.currentDeck[d];
            if (die?.faces == null)
                continue;
            for (var f = 0; f < die.faces.Length; f++)
            {
                var face = die.faces[f];
                if (face != null && face.type == DieType.Curse)
                    return true;
            }
        }

        return false;
    }

    public static int CountSocketedGems(UnknownMapEventEvaluationContext ctx)
    {
        var data = PlayerDataContainer.Instance?.RuntimeData;
        if (data?.currentDeck == null)
            return 0;
        var n = 0;
        for (var d = 0; d < data.currentDeck.Count; d++)
        {
            var die = data.currentDeck[d];
            if (die == null)
                continue;
            foreach (var g in die.GetSocketedGems())
            {
                if (g != null)
                    n++;
            }
        }

        return n;
    }
}

/// <summary>AND evaluation for <see cref="UnknownMapEventConditionBase"/> lists.</summary>
public static class UnknownMapEventConditionEvaluator
{
    public static bool AllPass(IReadOnlyList<UnknownMapEventConditionBase> conditions, UnknownMapEventEvaluationContext ctx)
    {
        if (conditions == null || conditions.Count == 0)
            return true;

        for (var i = 0; i < conditions.Count; i++)
        {
            var c = conditions[i];
            if (c == null)
            {
                Debug.LogError("UnknownMapEventConditionEvaluator: null condition entry in list — fix the asset.");
                return false;
            }

            if (!c.Evaluate(ctx))
                return false;
        }

        return true;
    }
}
