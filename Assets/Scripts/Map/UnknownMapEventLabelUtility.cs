using System;

/// <summary>Resolves dynamic placeholders in unknown map event option labels at display time.</summary>
public static class UnknownMapEventLabelUtility
{
    public static string ResolveOptionLabel(UnknownMapEventOptionEntry entry)
    {
        var label = entry?.label;
        if (string.IsNullOrWhiteSpace(label))
            return "Choose";

        label = label.Trim();
        if (label.IndexOf("{0}", StringComparison.Ordinal) < 0)
            return label;

        if (!TryGetPercentCurrencyCost(entry, out var currency, out var percent))
            return label;

        var amount = UnknownMapEventRunCurrencyUtility.ComputePercentCost(currency, percent);
        return string.Format(label, amount);
    }

    static bool TryGetPercentCurrencyCost(
        UnknownMapEventOptionEntry entry,
        out UnknownMapEventRunCurrency currency,
        out float percent)
    {
        currency = default;
        percent = 0f;

        var spend = FindFirstSpendRunCurrencyPercent(entry?.outcome);
        if (spend != null)
        {
            currency = spend.currency;
            percent = spend.percent;
            return true;
        }

        var conditions = entry?.enabledWhen;
        if (conditions == null)
            return false;

        for (var i = 0; i < conditions.Count; i++)
        {
            if (conditions[i] is UnknownMapEventConditionRunCurrencyPercentCostMin cond)
            {
                currency = cond.currency;
                percent = cond.percent;
                return true;
            }
        }

        return false;
    }

    static UnknownMapEventOutcomeSpendRunCurrencyPercent FindFirstSpendRunCurrencyPercent(UnknownMapEventOutcomeBase outcome)
    {
        if (outcome == null)
            return null;

        if (outcome is UnknownMapEventOutcomeSpendRunCurrencyPercent direct)
            return direct;

        if (outcome is UnknownMapEventOutcomeAfterDieChoice afterDieChoice && afterDieChoice.steps != null)
        {
            for (var i = 0; i < afterDieChoice.steps.Count; i++)
            {
                var found = FindFirstSpendRunCurrencyPercent(afterDieChoice.steps[i]);
                if (found != null)
                    return found;
            }
        }

        return null;
    }
}
