using System.Collections.Generic;

/// <summary>
/// Content IDs that appear in trial completion or rank-up unlock rewards stay out of loot until
/// <see cref="ProgressionRewardRegistry.RegisterUnlock"/> adds them to save data.
/// </summary>
public static class ProgressionGatedContent
{
    public static bool HasGatedContent(ProgressionCatalogSO catalog) =>
        catalog != null && GetGatedIds(catalog).Count > 0;

    public static bool IsGated(ProgressionCatalogSO catalog, string contentId)
    {
        if (catalog == null || ProgressionContentIds.IsNullOrEmpty(contentId))
            return false;
        return GetGatedIds(catalog).Contains(contentId);
    }

    public static HashSet<string> GetGatedIds(ProgressionCatalogSO catalog)
    {
        var gated = new HashSet<string>(System.StringComparer.Ordinal);
        if (catalog?.ranks == null)
            return gated;

        for (var r = 0; r < catalog.ranks.Count; r++)
        {
            var rank = catalog.ranks[r];
            if (rank == null)
                continue;

            if (rank.associatedTrials != null)
            {
                for (var t = 0; t < rank.associatedTrials.Count; t++)
                {
                    var trial = rank.associatedTrials[t];
                    if (trial?.completionReward != null)
                        CollectFromReward(trial.completionReward, gated);
                }
            }

            if (rank.rankUpRewards != null)
            {
                for (var i = 0; i < rank.rankUpRewards.Count; i++)
                {
                    var reward = rank.rankUpRewards[i];
                    if (reward != null)
                        CollectFromReward(reward, gated);
                }
            }
        }

        return gated;
    }

    static void CollectFromReward(ProgressionRewardBase reward, HashSet<string> gated)
    {
        switch (reward)
        {
            case ProgressionUnlockFacesReward unlockFaces when unlockFaces.faces != null:
                for (var i = 0; i < unlockFaces.faces.Count; i++)
                    AddIfPresent(gated, ProgressionContentIds.ForFace(unlockFaces.faces[i]));
                break;
            case ProgressionUnlockGemsReward unlockGems when unlockGems.gems != null:
                for (var i = 0; i < unlockGems.gems.Count; i++)
                    AddIfPresent(gated, ProgressionContentIds.ForGem(unlockGems.gems[i]));
                break;
            case ProgressionUnlockRelicsReward unlockRelics when unlockRelics.relics != null:
                for (var i = 0; i < unlockRelics.relics.Count; i++)
                    AddIfPresent(gated, ProgressionContentIds.ForRelic(unlockRelics.relics[i]));
                break;
            case ProgressionUnlockDiceReward unlockDice when unlockDice.dice != null:
                for (var i = 0; i < unlockDice.dice.Count; i++)
                    AddIfPresent(gated, ProgressionContentIds.ForDie(unlockDice.dice[i]));
                break;
            case ProgressionStartingRelicReward startingRelic:
                AddIfPresent(gated, ProgressionContentIds.ForRelic(startingRelic.relic));
                break;
        }
    }

    static void AddIfPresent(HashSet<string> gated, string contentId)
    {
        if (!ProgressionContentIds.IsNullOrEmpty(contentId))
            gated.Add(contentId);
    }
}
