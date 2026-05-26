using System.Collections.Generic;

/// <summary>
/// Resolves icon + row label for <see cref="ProgressionRewardBase"/> entries in trial reward tooltips.
/// </summary>
public static class ProgressionTrialRewardRowPresenter
{
    public readonly struct RowViewModel
    {
        public RowViewModel(UnityEngine.Sprite icon, string text)
        {
            Icon = icon;
            Text = text ?? string.Empty;
        }

        public UnityEngine.Sprite Icon { get; }
        public string Text { get; }
        public bool HasContent => Icon != null || !string.IsNullOrWhiteSpace(Text);
    }

    public static void CollectRows(
        GameIconIndexSO iconIndex,
        IReadOnlyList<ProgressionRewardBase> rewards,
        string trialCompletionRowFormatOverride,
        List<RowViewModel> into)
    {
        if (into == null || rewards == null)
            return;

        for (var i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null)
                continue;

            var row = BuildRow(iconIndex, reward, trialCompletionRowFormatOverride);
            if (row.HasContent)
                into.Add(row);
        }
    }

    public static RowViewModel BuildRow(
        GameIconIndexSO iconIndex,
        ProgressionRewardBase reward,
        string trialRowFormatOverride = null)
    {
        if (reward == null)
            return default;

        ResolveDisplay(reward, trialRowFormatOverride, out var placeholder, out var iconFromReward);
        var icon = iconFromReward ?? ResolveIcon(iconIndex, reward);
        var format = ResolveFormat(reward, trialRowFormatOverride);
        var text = FormatRowText(format, placeholder);
        return new RowViewModel(icon, text);
    }

    static void ResolveDisplay(
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        out string placeholder,
        out UnityEngine.Sprite iconOverride)
    {
        placeholder = string.Empty;
        iconOverride = null;

        switch (reward)
        {
            case ProgressionStartingRelicReward startingRelic:
                placeholder = startingRelic.relic != null && !string.IsNullOrWhiteSpace(startingRelic.relic.title)
                    ? startingRelic.relic.title.Trim()
                    : startingRelic.relic != null ? startingRelic.relic.name : string.Empty;
                iconOverride = startingRelic.relic != null ? startingRelic.relic.icon : null;
                break;
            case ProgressionUnlockRelicsReward unlockRelics:
                if (unlockRelics.relics != null && unlockRelics.relics.Count == 1 && unlockRelics.relics[0] != null)
                {
                    var relic = unlockRelics.relics[0];
                    placeholder = !string.IsNullOrWhiteSpace(relic.title) ? relic.title.Trim() : relic.name;
                    iconOverride = relic.icon;
                }
                else
                    placeholder = unlockRelics.relics != null ? unlockRelics.relics.Count.ToString() : "0";
                break;
            case ProgressionMaxHpReward hp:
                placeholder = hp.amount.ToString();
                break;
            case ProgressionMaxPowerReward power:
                placeholder = power.amount.ToString();
                break;
            case ProgressionStartingGoldReward gold:
                placeholder = gold.amount.ToString();
                break;
            case ProgressionMapMoveLimitReward moves:
                placeholder = moves.amount.ToString();
                break;
            case ProgressionMaxRollsReward maxRolls:
                placeholder = maxRolls.amount.ToString();
                break;
            case ProgressionExtraRollReward extraRoll:
                placeholder = extraRoll.amount.ToString();
                break;
            case ProgressionAddStartingDieReward addDie:
                if (addDie.die != null)
                {
                    placeholder = !string.IsNullOrWhiteSpace(addDie.die.dieName)
                        ? addDie.die.dieName.Trim()
                        : addDie.die.name;
                    iconOverride = addDie.die.uiIcon;
                }
                else
                    placeholder = string.Empty;
                break;
            case ProgressionUnlockDiceReward unlockDice:
                if (unlockDice.dice != null && unlockDice.dice.Count == 1 && unlockDice.dice[0] != null)
                {
                    var legacyDie = unlockDice.dice[0];
                    placeholder = !string.IsNullOrWhiteSpace(legacyDie.dieName)
                        ? legacyDie.dieName.Trim()
                        : legacyDie.name;
                    iconOverride = legacyDie.uiIcon;
                }
                else
                    placeholder = unlockDice.dice != null ? unlockDice.dice.Count.ToString() : "0";
                break;
            case ProgressionUnlockFacesReward unlockFaces:
                placeholder = unlockFaces.faces != null ? unlockFaces.faces.Count.ToString() : "0";
                break;
            case ProgressionUnlockGemsReward unlockGems:
                placeholder = unlockGems.gems != null ? unlockGems.gems.Count.ToString() : "0";
                break;
        }
    }

    static UnityEngine.Sprite ResolveIcon(GameIconIndexSO iconIndex, ProgressionRewardBase reward)
    {
        if (reward == null)
            return null;

        switch (reward)
        {
            case ProgressionMaxHpReward:
                return GetMain(iconIndex, MainAttributeIconId.Hp);
            case ProgressionMaxPowerReward:
                return GetMain(iconIndex, MainAttributeIconId.Power);
            case ProgressionStartingGoldReward:
                return GetMain(iconIndex, MainAttributeIconId.Coins);
            case ProgressionMapMoveLimitReward:
                return GetMain(iconIndex, MainAttributeIconId.Movement);
            case ProgressionMaxRollsReward:
            case ProgressionExtraRollReward:
                return GetMain(iconIndex, MainAttributeIconId.ExtraRoll);
            case ProgressionAddStartingDieReward addDie:
                return ResolveAddStartingDieIcon(iconIndex, addDie);
            case ProgressionUnlockDiceReward unlockDice:
                return ResolveLegacyUnlockDiceIcon(iconIndex, unlockDice);
            case ProgressionUnlockFacesReward:
                return GetMain(iconIndex, MainAttributeIconId.PhysicalDieUnlock);
            case ProgressionUnlockGemsReward:
                return GetMain(iconIndex, MainAttributeIconId.Power);
            case ProgressionUnlockRelicsReward unlockRelics:
                if (unlockRelics.relics != null && unlockRelics.relics.Count == 1)
                    return unlockRelics.relics[0] != null ? unlockRelics.relics[0].icon : null;
                return null;
            default:
                return null;
        }
    }

    static UnityEngine.Sprite ResolveLegacyUnlockDiceIcon(GameIconIndexSO iconIndex, ProgressionUnlockDiceReward unlockDice)
    {
        if (unlockDice?.dice == null || unlockDice.dice.Count == 0)
            return GetMain(iconIndex, MainAttributeIconId.PhysicalDieUnlock);

        for (var i = 0; i < unlockDice.dice.Count; i++)
        {
            var die = unlockDice.dice[i];
            if (die == null)
                continue;

            if (die.uiIcon != null)
                return die.uiIcon;

            var sprite = iconIndex != null
                ? iconIndex.GetDieUnlockMainAttributeIcon(die.dieType)
                : GameIconCatalog.GetDieUnlockMainAttributeIcon(die.dieType);
            if (sprite != null)
                return sprite;
        }

        return GetMain(iconIndex, MainAttributeIconId.PhysicalDieUnlock);
    }

    static UnityEngine.Sprite ResolveAddStartingDieIcon(GameIconIndexSO iconIndex, ProgressionAddStartingDieReward addDie)
    {
        if (addDie?.die != null && addDie.die.uiIcon != null)
            return addDie.die.uiIcon;

        if (addDie?.die != null)
        {
            var sprite = iconIndex != null
                ? iconIndex.GetDieUnlockMainAttributeIcon(addDie.die.dieType)
                : GameIconCatalog.GetDieUnlockMainAttributeIcon(addDie.die.dieType);
            if (sprite != null)
                return sprite;
        }

        return GetMain(iconIndex, MainAttributeIconId.PhysicalDieUnlock);
    }

    static UnityEngine.Sprite GetMain(GameIconIndexSO iconIndex, MainAttributeIconId id)
    {
        if (iconIndex != null)
            return iconIndex.GetMainAttributeIcon(id);
        return GameIconCatalog.GetMainAttributeIcon(id);
    }

    static string ResolveFormat(ProgressionRewardBase reward, string trialRowFormatOverride)
    {
        if (!string.IsNullOrWhiteSpace(trialRowFormatOverride))
            return trialRowFormatOverride.Trim();

        if (!string.IsNullOrWhiteSpace(reward.rowFormat))
            return reward.rowFormat.Trim();

        return GetDefaultFormat(reward);
    }

    static string GetDefaultFormat(ProgressionRewardBase reward) => reward switch
    {
        ProgressionMaxHpReward => "+{0} Max HP",
        ProgressionMaxPowerReward => "+{0} Max Power",
        ProgressionStartingGoldReward => "+{0} Starting Gold",
        ProgressionMapMoveLimitReward => "+{0} Map Moves",
        ProgressionMaxRollsReward maxRolls => maxRolls.amount == 1
            ? "Gain +1 Extra Roll"
            : "Gain +{0} Extra Rolls",
        ProgressionExtraRollReward extra => extra.amount == 1
            ? "Gain +1 Extra Roll"
            : "Gain +{0} Extra Rolls",
        ProgressionStartingRelicReward => "Start with {0}",
        ProgressionUnlockFacesReward r => r.faces != null && r.faces.Count == 1
            ? "Unlock face"
            : "Unlock {0} faces",
        ProgressionUnlockGemsReward r => r.gems != null && r.gems.Count == 1
            ? "Unlock gem"
            : "Unlock {0} gems",
        ProgressionUnlockRelicsReward r => r.relics != null && r.relics.Count == 1
            ? "Unlock {0}"
            : "Unlock {0} relics",
        ProgressionAddStartingDieReward => "Add {0} to deck",
        ProgressionUnlockDiceReward r => r.dice != null && r.dice.Count == 1
            ? "Add {0} to deck"
            : "Add {0} dice to deck",
        _ => "{0}"
    };

    static string FormatRowText(string format, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(format))
            return placeholder ?? string.Empty;

        try
        {
            return string.Format(format, placeholder ?? string.Empty);
        }
        catch (System.FormatException)
        {
            return format.Replace("{0}", placeholder ?? string.Empty);
        }
    }
}
