using System.Collections.Generic;

/// <summary>Collects unlock rewards from trial completion lists for celebration popups.</summary>
public static class ProgressionUnlockCelebrationContent
{
    public static void CollectFromTrial(PlayerTrialSO trial, List<ProgressionUnlockedContentItem> items)
    {
        items?.Clear();
        if (trial?.completionRewards == null)
            return;

        CollectFromRewards(trial.completionRewards, items);
    }

    public static void CollectFromRewards(
        IReadOnlyList<ProgressionRewardBase> rewards,
        List<ProgressionUnlockedContentItem> items)
    {
        if (rewards == null || items == null)
            return;

        var seenFaces = new HashSet<DieFaceSO>();
        var seenRelics = new HashSet<RelicSO>();

        for (var i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null)
                continue;

            switch (reward)
            {
                case ProgressionUnlockFacesReward unlockFaces when unlockFaces.faces != null:
                    for (var f = 0; f < unlockFaces.faces.Count; f++)
                    {
                        var face = unlockFaces.faces[f];
                        if (face == null || !seenFaces.Add(face))
                            continue;

                        var item = ProgressionUnlockedContentItem.FromFace(face);
                        if (item.IsValid)
                            items.Add(item);
                    }

                    break;
                case ProgressionUnlockRelicsReward unlockRelics when unlockRelics.relics != null:
                    for (var r = 0; r < unlockRelics.relics.Count; r++)
                    {
                        var relic = unlockRelics.relics[r];
                        if (relic == null || !seenRelics.Add(relic))
                            continue;

                        var item = ProgressionUnlockedContentItem.FromRelic(relic);
                        if (item.IsValid)
                            items.Add(item);
                    }

                    break;
            }
        }
    }
}
