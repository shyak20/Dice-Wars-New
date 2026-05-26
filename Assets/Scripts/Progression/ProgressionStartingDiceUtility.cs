using System.Collections.Generic;
using UnityEngine;

/// <summary>Starting dice granted by <see cref="ProgressionAddStartingDieReward"/>.</summary>
public static class ProgressionStartingDiceUtility
{
    /// <summary>Records a grant and appends one die copy to the character template and active runtime deck.</summary>
    public static void ApplyAddedDie(
        ProgressionProfileSaveData save,
        DieAssetSO die,
        PlayerDataSO characterTemplate,
        ProgressionCatalogSO catalog = null)
    {
        if (save == null || die == null)
            return;

        characterTemplate ??= ResolveCharacterTemplate();
        if (characterTemplate == null)
            return;

        catalog ??= ResolveCatalog(characterTemplate);
        RecordGrantedDie(save, die);

        if (catalog != null)
            ReconcileGrantedDiceOnTemplate(catalog, save, characterTemplate);
        else
            AppendOneGrantedDieToTemplate(characterTemplate, die);

        save.grantedDiceDeckEntriesApplied = save.addedStartingDieIds.Count;
        AppendCloneToActiveRuntimeDeck(characterTemplate, die);
    }

    /// <summary>Appends deck entries for any recorded grants not yet written to <paramref name="template"/>.</summary>
    public static void ReconcileGrantedDiceOnTemplate(
        ProgressionCatalogSO catalog,
        ProgressionProfileSaveData save,
        PlayerDataSO template)
    {
        if (catalog == null || save == null || template == null)
            return;

        save.addedStartingDieIds ??= new List<string>();
        template.currentDeck ??= new List<DieAssetSO>();

        var applied = Mathf.Max(0, save.grantedDiceDeckEntriesApplied);
        for (var i = applied; i < save.addedStartingDieIds.Count; i++)
        {
            var id = save.addedStartingDieIds[i];
            if (!TryResolveDie(catalog, id, out var die) || die == null)
                continue;

            template.currentDeck.Add(die);
        }

        save.grantedDiceDeckEntriesApplied = save.addedStartingDieIds.Count;
        MarkTemplateDirty(template);
    }

    public static void RecordGrantedDie(ProgressionProfileSaveData save, DieAssetSO die)
    {
        if (save == null || die == null)
            return;

        var id = ProgressionContentIds.ForDie(die);
        if (ProgressionContentIds.IsNullOrEmpty(id))
            return;

        save.addedStartingDieIds ??= new List<string>();
        save.addedStartingDieIds.Add(id);
    }

    static void AppendOneGrantedDieToTemplate(PlayerDataSO template, DieAssetSO die)
    {
        if (template == null || die == null)
            return;

        template.currentDeck ??= new List<DieAssetSO>();
        template.currentDeck.Add(die);
        MarkTemplateDirty(template);
    }

    public static void CollectDiceFromRewards(IReadOnlyList<ProgressionRewardBase> rewards, List<DieAssetSO> into)
    {
        if (rewards == null || into == null)
            return;

        for (var r = 0; r < rewards.Count; r++)
        {
            var reward = rewards[r];
            switch (reward)
            {
                case ProgressionAddStartingDieReward add when add.die != null:
                    into.Add(add.die);
                    break;
                case ProgressionUnlockDiceReward unlock when unlock.dice != null:
                    for (var i = 0; i < unlock.dice.Count; i++)
                    {
                        if (unlock.dice[i] != null)
                            into.Add(unlock.dice[i]);
                    }
                    break;
            }
        }
    }

    public static bool TryResolveDie(ProgressionCatalogSO catalog, string contentId, out DieAssetSO die)
    {
        die = null;
        if (catalog?.ranks == null || ProgressionContentIds.IsNullOrEmpty(contentId))
            return false;

        for (var r = 0; r < catalog.ranks.Count; r++)
        {
            var rank = catalog.ranks[r];
            if (rank == null)
                continue;

            if (rank.associatedTrials != null)
            {
                for (var t = 0; t < rank.associatedTrials.Count; t++)
                {
                    if (TryGetDieFromReward(rank.associatedTrials[t]?.completionReward, contentId, out die))
                        return true;
                }
            }

            if (rank.rankUpRewards != null)
            {
                for (var i = 0; i < rank.rankUpRewards.Count; i++)
                {
                    if (TryGetDieFromReward(rank.rankUpRewards[i], contentId, out die))
                        return true;
                }
            }
        }

        return false;
    }

    static bool TryGetDieFromReward(ProgressionRewardBase reward, string contentId, out DieAssetSO die)
    {
        die = null;
        if (reward == null || ProgressionContentIds.IsNullOrEmpty(contentId))
            return false;

        if (reward is ProgressionAddStartingDieReward add && add.die != null)
        {
            if (!string.Equals(ProgressionContentIds.ForDie(add.die), contentId, System.StringComparison.Ordinal))
                return false;
            die = add.die;
            return true;
        }

        if (reward is ProgressionUnlockDiceReward unlock && unlock.dice != null)
        {
            for (var i = 0; i < unlock.dice.Count; i++)
            {
                var entry = unlock.dice[i];
                if (entry == null)
                    continue;
                if (!string.Equals(ProgressionContentIds.ForDie(entry), contentId, System.StringComparison.Ordinal))
                    continue;
                die = entry;
                return true;
            }
        }

        return false;
    }

    public static bool DeckContainsDieByName(List<DieAssetSO> deck, string dieAssetName)
    {
        if (deck == null || string.IsNullOrEmpty(dieAssetName))
            return false;

        for (var i = 0; i < deck.Count; i++)
        {
            var entry = deck[i];
            if (entry != null && entry.name == dieAssetName)
                return true;
        }

        return false;
    }

    static PlayerDataSO ResolveCharacterTemplate()
    {
        var progression = ProgressionManager.TryGetRuntime();
        if (progression != null && progression.ActiveCharacterTemplate != null)
            return progression.ActiveCharacterTemplate;

        var container = PlayerDataContainer.Instance;
        return container != null ? container.ActiveCharacterTemplate : null;
    }

    static ProgressionCatalogSO ResolveCatalog(PlayerDataSO template)
    {
        if (template?.progressionCatalog != null)
            return template.progressionCatalog;

        return ProgressionManager.TryGetRuntime()?.Catalog;
    }

    static void AppendCloneToActiveRuntimeDeck(PlayerDataSO template, DieAssetSO die)
    {
        if (template == null || die == null)
            return;

        var container = PlayerDataContainer.Instance;
        if (container == null || container.ActiveCharacterTemplate != template || container.RuntimeData == null)
            return;

        container.AddDieToDeck(die);
    }

    static void MarkTemplateDirty(PlayerDataSO template)
    {
#if UNITY_EDITOR
        if (template == null)
            return;
        UnityEditor.EditorUtility.SetDirty(template);
#endif
    }

    public static List<DieAssetSO> CopyDeckReferences(IReadOnlyList<DieAssetSO> source)
    {
        var deck = new List<DieAssetSO>();
        if (source == null)
            return deck;

        for (var i = 0; i < source.Count; i++)
        {
            var die = source[i];
            if (die != null)
                deck.Add(die);
        }

        return deck;
    }
}
