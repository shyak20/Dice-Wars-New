using System.Collections.Generic;
using System.Text;

/// <summary>Human-readable summaries for progression reward assets (celebration popups).</summary>
public static class ProgressionRewardDescriptionUtility
{
    public static string Describe(ProgressionRewardBase reward)
    {
        if (reward == null)
            return string.Empty;

        switch (reward)
        {
            case ProgressionMaxHpReward r:
                return $"+{r.amount} Max HP";
            case ProgressionMaxPowerReward r:
                return $"+{r.amount} Max Power";
            case ProgressionStartingGoldReward r:
                return $"+{r.amount} Starting Gold";
            case ProgressionMapMoveLimitReward r:
                return $"+{r.amount} Map Moves";
            case ProgressionMaxRollsReward maxRolls:
                return DescribeExtraRollGain(maxRolls.amount);
            case ProgressionExtraRollReward extraRoll:
                return DescribeExtraRollGain(extraRoll.amount);
            case ProgressionUnlockFacesReward r:
                return DescribeUnlockCount("face", r.faces?.Count ?? 0);
            case ProgressionUnlockGemsReward r:
                return DescribeUnlockCount("gem", r.gems?.Count ?? 0);
            case ProgressionUnlockRelicsReward r:
                return DescribeUnlockCount("relic", r.relics?.Count ?? 0);
            case ProgressionStartingRelicReward r:
                return r.relic != null && !string.IsNullOrWhiteSpace(r.relic.title)
                    ? $"Start each run with {r.relic.title.Trim()}"
                    : "Start each run with relic";
            case ProgressionUnlockDiceReward r:
                return DescribeUnlockCount("die", r.dice?.Count ?? 0);
            default:
                return reward.GetType().Name;
        }
    }

    public static string DescribeList(IReadOnlyList<ProgressionRewardBase> rewards)
    {
        if (rewards == null || rewards.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < rewards.Count; i++)
        {
            var line = Describe(rewards[i]);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append("• ");
            sb.Append(line);
        }

        return sb.ToString();
    }

    static string DescribeUnlockCount(string label, int count) =>
        count <= 0 ? $"Unlock {label}" : $"Unlock {count} {label}(s)";

    static string DescribeExtraRollGain(int amount) =>
        amount == 1 ? "Gain +1 Extra Roll" : $"Gain +{amount} Extra Rolls";
}
