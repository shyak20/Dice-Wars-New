using UnityEngine;

/// <summary>Run resources that unknown map events can read or spend by percentage.</summary>
public enum UnknownMapEventRunCurrency
{
    Coins,
    CurrentHp,
    MaxHp,
    MaxPower,
}

/// <summary>Shared read/spend helpers for <see cref="UnknownMapEventRunCurrency"/>.</summary>
public static class UnknownMapEventRunCurrencyUtility
{
    public static int GetCurrentAmount(UnknownMapEventRunCurrency currency)
    {
        switch (currency)
        {
            case UnknownMapEventRunCurrency.Coins:
                return RunEconomyManager.Instance != null ? RunEconomyManager.Instance.CurrentGold : 0;
            case UnknownMapEventRunCurrency.CurrentHp:
                return RunManager.Instance != null ? RunManager.Instance.RunCurrentHp : 0;
            case UnknownMapEventRunCurrency.MaxHp:
                return RunManager.Instance != null ? RunManager.Instance.RunMaxHp : 0;
            case UnknownMapEventRunCurrency.MaxPower:
            {
                var data = PlayerDataContainer.Instance?.RuntimeData;
                return data != null ? PlayerMaxPowerForRun.Compute(data) : 0;
            }
            default:
                return 0;
        }
    }

    /// <summary>Rounded cost for <paramref name="percent"/> of <paramref name="currentAmount"/> (0–100).</summary>
    public static int ComputePercentCost(int currentAmount, float percent)
    {
        if (currentAmount <= 0 || percent <= 0f)
            return 0;
        return Mathf.Max(0, Mathf.RoundToInt(currentAmount * (percent / 100f)));
    }

    public static int ComputePercentCost(UnknownMapEventRunCurrency currency, float percent) =>
        ComputePercentCost(GetCurrentAmount(currency), percent);

    public static bool TrySpendAbsolute(UnknownMapEventRunCurrency currency, int amount)
    {
        if (amount <= 0)
            return true;

        var current = GetCurrentAmount(currency);
        if (amount > current)
            return false;

        switch (currency)
        {
            case UnknownMapEventRunCurrency.Coins:
            {
                var eco = RunEconomyManager.Instance;
                if (eco == null)
                {
                    Debug.LogError("UnknownMapEventRunCurrencyUtility: RunEconomyManager missing.");
                    return false;
                }

                return eco.TrySpend(amount);
            }
            case UnknownMapEventRunCurrency.CurrentHp:
            {
                var rm = RunManager.Instance;
                if (rm == null)
                {
                    Debug.LogError("UnknownMapEventRunCurrencyUtility: RunManager missing.");
                    return false;
                }

                rm.ApplyRunCurrentHpDelta(-amount);
                return true;
            }
            case UnknownMapEventRunCurrency.MaxHp:
            {
                var rm = RunManager.Instance;
                if (rm == null)
                {
                    Debug.LogError("UnknownMapEventRunCurrencyUtility: RunManager missing.");
                    return false;
                }

                rm.ApplyRunMaxHpDelta(-amount);
                return true;
            }
            case UnknownMapEventRunCurrency.MaxPower:
            {
                var rm = RunManager.Instance;
                if (rm == null)
                {
                    Debug.LogError("UnknownMapEventRunCurrencyUtility: RunManager missing.");
                    return false;
                }

                rm.ApplyRunMaxPowerBudgetDelta(-amount);
                return true;
            }
            default:
                return false;
        }
    }

    public static bool TrySpendPercent(UnknownMapEventRunCurrency currency, float percent, out int amountSpent)
    {
        amountSpent = ComputePercentCost(currency, percent);
        return TrySpendAbsolute(currency, amountSpent);
    }
}
