using System.Collections.Generic;

/// <summary>Unlock IDs and vertical bonus aggregation for <see cref="ProgressionRewardBase"/> instances.</summary>
public static class ProgressionRewardRegistry
{
    public static void Apply(ProgressionProfileSaveData save, ProgressionRewardBase reward)
    {
        if (save == null || reward == null)
            return;

        reward.Apply(new ProgressionRewardApplyContext { Save = save });
    }

    public static void RegisterUnlock(ProgressionProfileSaveData save, string contentId)
    {
        if (save == null || ProgressionContentIds.IsNullOrEmpty(contentId))
            return;

        save.unlockedContentIDs ??= new List<string>();
        for (var i = 0; i < save.unlockedContentIDs.Count; i++)
        {
            if (string.Equals(save.unlockedContentIDs[i], contentId, System.StringComparison.Ordinal))
                return;
        }

        save.unlockedContentIDs.Add(contentId);
    }

    public static void RegisterUnlocks(ProgressionProfileSaveData save, IEnumerable<string> contentIds)
    {
        if (contentIds == null)
            return;

        foreach (var id in contentIds)
            RegisterUnlock(save, id);
    }

    public static int SumBonus(IReadOnlyList<ProgressionRewardBase> rewards, System.Type bonusType)
    {
        if (rewards == null || bonusType == null)
            return 0;

        var total = 0;
        for (var i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null || reward.GetType() != bonusType)
                continue;
            total += GetAmount(reward);
        }

        return total;
    }

    static int GetAmount(ProgressionRewardBase reward) => reward switch
    {
        ProgressionMaxHpReward hp => hp.amount,
        ProgressionMaxPowerReward power => power.amount,
        ProgressionStartingGoldReward gold => gold.amount,
        ProgressionMapMoveLimitReward moves => moves.amount,
        ProgressionMaxRollsReward rolls => rolls.amount,
        _ => 0
    };
}
