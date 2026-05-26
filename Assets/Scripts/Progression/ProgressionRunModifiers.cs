using System.Collections.Generic;
using UnityEngine;

/// <summary>Aggregates vertical bonuses and unlock queries from catalog + save.</summary>
public static class ProgressionRunModifiers
{
    public static int SumMaxHp(ProgressionCatalogSO catalog, ProgressionProfileSaveData save) =>
        SumStatBonus(catalog, save, typeof(ProgressionMaxHpReward));

    public static int SumMaxPower(ProgressionCatalogSO catalog, ProgressionProfileSaveData save) =>
        SumStatBonus(catalog, save, typeof(ProgressionMaxPowerReward));

    public static int SumStartingGold(ProgressionCatalogSO catalog, ProgressionProfileSaveData save) =>
        SumStatBonus(catalog, save, typeof(ProgressionStartingGoldReward));

    public static int SumMapMoveLimit(ProgressionCatalogSO catalog, ProgressionProfileSaveData save) =>
        SumStatBonus(catalog, save, typeof(ProgressionMapMoveLimitReward));

    public static int SumExtraRolls(ProgressionCatalogSO catalog, ProgressionProfileSaveData save) =>
        SumExtraRollBonus(catalog, save);

    public static int SumMaxRolls(ProgressionCatalogSO catalog, ProgressionProfileSaveData save) =>
        SumExtraRolls(catalog, save);

    public static int SumExtraRollBonus(ProgressionCatalogSO catalog, ProgressionProfileSaveData save)
    {
        var rewards = new List<ProgressionRewardBase>();
        CollectGrantedRewards(catalog, save, rewards);

        var total = 0;
        for (var i = 0; i < rewards.Count; i++)
            total += GetExtraRollAmount(rewards[i]);

        return total;
    }

    public static int GetExtraRollAmount(ProgressionRewardBase reward) => reward switch
    {
        ProgressionExtraRollReward extra => extra.amount,
        ProgressionMaxRollsReward maxRolls => maxRolls.amount,
        _ => 0
    };

    /// <summary>Relics granted at run start from completed rank-up / trial rewards.</summary>
    public static void CollectStartingRelics(
        ProgressionCatalogSO catalog,
        ProgressionProfileSaveData save,
        List<RelicSO> into)
    {
        if (into == null)
            return;

        var rewards = new List<ProgressionRewardBase>();
        CollectGrantedRewards(catalog, save, rewards);

        for (var i = 0; i < rewards.Count; i++)
        {
            if (rewards[i] is not ProgressionStartingRelicReward startingRelic || startingRelic.relic == null)
                continue;

            if (!into.Contains(startingRelic.relic))
                into.Add(startingRelic.relic);
        }
    }

    static int SumStatBonus(ProgressionCatalogSO catalog, ProgressionProfileSaveData save, System.Type statRewardType)
    {
        var rewards = new List<ProgressionRewardBase>();
        CollectGrantedRewards(catalog, save, rewards);
        return ProgressionRewardRegistry.SumBonus(rewards, statRewardType);
    }

    public static void CollectGrantedRewards(
        ProgressionCatalogSO catalog,
        ProgressionProfileSaveData save,
        List<ProgressionRewardBase> buffer)
    {
        if (buffer == null)
            return;

        if (catalog != null && save?.completedTrialIDs != null)
        {
            for (var i = 0; i < save.completedTrialIDs.Count; i++)
            {
                var trial = FindTrialById(catalog, save.completedTrialIDs[i]);
                if (trial?.completionReward != null)
                    buffer.Add(trial.completionReward);
            }
        }

        if (catalog == null || save == null)
            return;

        var appliedThrough = save.rankUpRewardsAppliedThroughRankIndex;
        if (save.pendingRankUpCelebration
            && catalog.TryGetRank(save.currentRankIndex, out var pendingRank)
            && pendingRank != null)
        {
            appliedThrough = Mathf.Max(appliedThrough, pendingRank.rankIndex);
        }

        if (appliedThrough < 0)
            return;

        for (var r = 0; r <= appliedThrough; r++)
        {
            if (!catalog.TryGetRank(r, out var rank) || rank?.rankUpRewards == null)
                continue;

            for (var i = 0; i < rank.rankUpRewards.Count; i++)
            {
                var reward = rank.rankUpRewards[i];
                if (reward != null)
                    buffer.Add(reward);
            }
        }
    }

    public static bool IsContentUnlocked(
        ProgressionCatalogSO catalog,
        ProgressionProfileSaveData save,
        string contentId)
    {
        if (ProgressionContentIds.IsNullOrEmpty(contentId))
            return false;

        if (catalog != null && catalog.IsAlwaysAvailable(contentId))
            return true;

        if (catalog != null && !ProgressionGatedContent.IsGated(catalog, contentId))
            return true;

        if (save?.unlockedContentIDs == null)
            return false;

        for (var i = 0; i < save.unlockedContentIDs.Count; i++)
        {
            if (string.Equals(save.unlockedContentIDs[i], contentId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool HasHorizontalUnlocks(ProgressionCatalogSO catalog) =>
        ProgressionGatedContent.HasGatedContent(catalog);

    public static HashSet<string> BuildUnlockedSet(ProgressionProfileSaveData save)
    {
        var set = new HashSet<string>(System.StringComparer.Ordinal);
        if (save?.unlockedContentIDs == null)
            return set;

        for (var i = 0; i < save.unlockedContentIDs.Count; i++)
        {
            var id = save.unlockedContentIDs[i];
            if (!ProgressionContentIds.IsNullOrEmpty(id))
                set.Add(id);
        }

        return set;
    }

    public static PlayerTrialSO FindTrialById(ProgressionCatalogSO catalog, string trialId)
    {
        if (catalog?.ranks == null || ProgressionContentIds.IsNullOrEmpty(trialId))
            return null;

        for (var r = 0; r < catalog.ranks.Count; r++)
        {
            var rank = catalog.ranks[r];
            if (rank?.associatedTrials == null)
                continue;

            for (var t = 0; t < rank.associatedTrials.Count; t++)
            {
                var trial = rank.associatedTrials[t];
                if (trial != null && string.Equals(trial.trialID, trialId, System.StringComparison.Ordinal))
                    return trial;
            }
        }

        return null;
    }
}
