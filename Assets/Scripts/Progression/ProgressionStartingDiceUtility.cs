using System.Collections.Generic;
using UnityEngine;

/// <summary>Starting dice granted by <see cref="ProgressionAddStartingDieReward"/> (PlayerPrefs only).</summary>
public static class ProgressionStartingDiceUtility
{
    /// <summary>Records a grant in progression save (PlayerPrefs). Applied to the deck on the next run via <see cref="BuildEffectiveDeck"/>.</summary>
    public static void ApplyAddedDie(ProgressionProfileSaveData save, DieAssetSO die)
    {
        if (save == null || die == null)
            return;

        RecordGrantedDie(save, die);
    }

    /// <summary>Base <see cref="PlayerDataSO.currentDeck"/> plus one entry per saved grant.</summary>
    public static List<DieAssetSO> BuildEffectiveDeck(
        ProgressionCatalogSO catalog,
        ProgressionProfileSaveData save,
        PlayerDataSO template)
    {
        var deck = CopyDeckReferences(template != null ? template.currentDeck : null);
        if (catalog == null || save == null)
            return deck;

        var grants = save.grantedStartingDice;
        if (grants == null || grants.Count == 0)
            return deck;

        for (var i = 0; i < grants.Count; i++)
        {
            var entry = grants[i];
            if (ProgressionContentIds.IsNullOrEmpty(entry.dieAssetId))
                continue;
            if (!TryResolveDie(catalog, entry.dieAssetId, out var die) || die == null)
                continue;
            deck.Add(die);
        }

        return deck;
    }

    /// <summary>Removes grant copies that older builds appended to the PlayerDataSO asset file.</summary>
    public static void StripLegacyGrantsFromTemplate(
        ProgressionCatalogSO catalog,
        ProgressionProfileSaveData save,
        PlayerDataSO template)
    {
        if (catalog == null || save == null || template == null || save.legacyTemplateGrantsStripped)
            return;

        var grants = save.grantedStartingDice;
        if (grants == null || grants.Count == 0)
        {
            save.legacyTemplateGrantsStripped = true;
            return;
        }

        template.currentDeck ??= new List<DieAssetSO>();
        for (var g = grants.Count - 1; g >= 0; g--)
        {
            var entry = grants[g];
            if (ProgressionContentIds.IsNullOrEmpty(entry.dieAssetId))
                continue;
            if (!TryResolveDie(catalog, entry.dieAssetId, out var die) || die == null)
                continue;

            for (var d = template.currentDeck.Count - 1; d >= 0; d--)
            {
                var deckDie = template.currentDeck[d];
                if (deckDie != null && deckDie.name == die.name)
                {
                    template.currentDeck.RemoveAt(d);
                    break;
                }
            }
        }

        save.legacyTemplateGrantsStripped = true;
        MarkTemplateDirty(template);
    }

    public static void RecordGrantedDie(ProgressionProfileSaveData save, DieAssetSO die)
    {
        if (save == null || die == null)
            return;

        var id = ProgressionContentIds.ForDie(die);
        if (ProgressionContentIds.IsNullOrEmpty(id))
            return;

        save.grantedStartingDice ??= new List<GrantedStartingDieSaveEntry>();
        save.grantedStartingDice.Add(new GrantedStartingDieSaveEntry
        {
            dieAssetId = id,
            dieType = die.dieType
        });
    }

    public static void UpgradeGrantEntryDieTypes(ProgressionCatalogSO catalog, ProgressionProfileSaveData save)
    {
        if (catalog == null || save?.grantedStartingDice == null)
            return;

        for (var i = 0; i < save.grantedStartingDice.Count; i++)
        {
            var entry = save.grantedStartingDice[i];
            if (!TryResolveDie(catalog, entry.dieAssetId, out var die) || die == null)
                continue;

            entry.dieType = die.dieType;
            save.grantedStartingDice[i] = entry;
        }
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

    public static bool TryResolveDie(ProgressionCatalogSO catalog, string dieAssetId, out DieAssetSO die)
    {
        die = null;
        if (catalog?.ranks == null || ProgressionContentIds.IsNullOrEmpty(dieAssetId))
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
                    var trialRewards = rank.associatedTrials[t]?.completionRewards;
                    if (trialRewards != null)
                    {
                        for (var i = 0; i < trialRewards.Count; i++)
                        {
                            if (TryGetDieFromReward(trialRewards[i], dieAssetId, out die))
                                return true;
                        }
                    }
                }
            }

            if (rank.rankUpRewards != null)
            {
                for (var i = 0; i < rank.rankUpRewards.Count; i++)
                {
                    if (TryGetDieFromReward(rank.rankUpRewards[i], dieAssetId, out die))
                        return true;
                }
            }
        }

        return false;
    }

    static bool TryGetDieFromReward(ProgressionRewardBase reward, string dieAssetId, out DieAssetSO die)
    {
        die = null;
        if (reward == null || ProgressionContentIds.IsNullOrEmpty(dieAssetId))
            return false;

        if (reward is ProgressionAddStartingDieReward add && add.die != null)
        {
            if (!string.Equals(ProgressionContentIds.ForDie(add.die), dieAssetId, System.StringComparison.Ordinal))
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
                if (!string.Equals(ProgressionContentIds.ForDie(entry), dieAssetId, System.StringComparison.Ordinal))
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
