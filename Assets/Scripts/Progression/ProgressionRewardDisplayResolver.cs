using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProgressionRewardExpandMode
{
    /// <summary>One compact row per reward entry (trial hover tooltip).</summary>
    PerReward,

    /// <summary>Celebration popups: stat/item rows expanded; face unlocks grouped by <see cref="DieType"/>.</summary>
    Celebration,
}

public enum ProgressionRewardDisplayStyle
{
    Stat,
    ItemDetail,
    Compact,
}

/// <summary>Resolved row for progression reward UIs.</summary>
public struct ProgressionRewardDisplayEntry
{
    public ProgressionRewardVisualKind kind;
    public ProgressionRewardDisplayStyle style;
    public Sprite icon;
    public string text;
    public MainAttributeIconId? mainAttributeIconId;
    public int? statAmount;
    public string statLabel;
    public RelicSO relic;
    public GemSO gem;
    public DieAssetSO die;
    public DieFaceSO face;
    public DieType? faceUnlockDieType;
    public ProgressionRewardBase sourceReward;

    public bool HasContent =>
        style == ProgressionRewardDisplayStyle.Stat
        || style == ProgressionRewardDisplayStyle.ItemDetail
        || icon != null
        || !string.IsNullOrWhiteSpace(text);

    public static ProgressionRewardDisplayEntry Compact(
        ProgressionRewardVisualKind kind,
        Sprite icon,
        string text,
        ProgressionRewardBase sourceReward = null) =>
        new ProgressionRewardDisplayEntry
        {
            kind = kind,
            style = ProgressionRewardDisplayStyle.Compact,
            icon = icon,
            text = text ?? string.Empty,
            sourceReward = sourceReward,
        };
}

/// <summary>
/// Central resolver for progression reward icons, labels, and celebration vs tooltip expansion.
/// </summary>
public static class ProgressionRewardDisplayResolver
{
    public static void ExpandRewards(
        IReadOnlyList<ProgressionRewardBase> rewards,
        ProgressionRewardVisualCatalogSO catalog,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        List<ProgressionRewardDisplayEntry> into)
    {
        if (into == null || rewards == null)
            return;

        for (var i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null)
                continue;

            ExpandSingleReward(reward, catalog, trialRowFormatOverride, mode, into);
        }
    }

    static void ExpandSingleReward(
        ProgressionRewardBase reward,
        ProgressionRewardVisualCatalogSO catalog,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        List<ProgressionRewardDisplayEntry> into)
    {
        switch (reward)
        {
            case ProgressionMaxHpReward hp:
                AddStatOrCompact(into, catalog, reward, trialRowFormatOverride, mode,
                    ProgressionRewardVisualKind.MaxHp, MainAttributeIconId.Hp, "Max Health", hp.amount);
                break;
            case ProgressionMaxPowerReward power:
                AddStatOrCompact(into, catalog, reward, trialRowFormatOverride, mode,
                    ProgressionRewardVisualKind.MaxPower, MainAttributeIconId.Power, "Base Max Power", power.amount);
                break;
            case ProgressionStartingGoldReward gold:
                AddStatOrCompact(into, catalog, reward, trialRowFormatOverride, mode,
                    ProgressionRewardVisualKind.StartingGold, MainAttributeIconId.Coins, "Starting Gold", gold.amount);
                break;
            case ProgressionMapMoveLimitReward moves:
                AddStatOrCompact(into, catalog, reward, trialRowFormatOverride, mode,
                    ProgressionRewardVisualKind.MapMoves, MainAttributeIconId.Movement, "Map Moves", moves.amount);
                break;
            case ProgressionMaxRollsReward maxRolls:
                AddStatOrCompact(into, catalog, reward, trialRowFormatOverride, mode,
                    ProgressionRewardVisualKind.ExtraRoll, MainAttributeIconId.ExtraRoll, "Max Rolls", maxRolls.amount);
                break;
            case ProgressionExtraRollReward extraRoll:
                AddStatOrCompact(into, catalog, reward, trialRowFormatOverride, mode,
                    ProgressionRewardVisualKind.ExtraRoll, MainAttributeIconId.ExtraRoll, "Extra Rolls", extraRoll.amount);
                break;
            case ProgressionStartingRelicReward startingRelic:
                AddRelicEntries(into, catalog, reward, trialRowFormatOverride, mode, startingRelic.relic);
                break;
            case ProgressionUnlockRelicsReward unlockRelics:
                AddRelicListEntries(into, catalog, reward, trialRowFormatOverride, mode, unlockRelics.relics);
                break;
            case ProgressionUnlockGemsReward unlockGems:
                AddGemListEntries(into, catalog, reward, trialRowFormatOverride, mode, unlockGems.gems);
                break;
            case ProgressionUnlockFacesReward unlockFaces:
                AddFaceEntries(into, catalog, reward, trialRowFormatOverride, mode, unlockFaces.faces);
                break;
            case ProgressionAddStartingDieReward addDie:
                AddDieEntries(into, catalog, reward, trialRowFormatOverride, mode, addDie.die);
                break;
            case ProgressionUnlockDiceReward unlockDice:
                AddLegacyDiceEntries(into, catalog, reward, trialRowFormatOverride, mode, unlockDice.dice);
                break;
            default:
                into.Add(ProgressionRewardDisplayEntry.Compact(
                    ProgressionRewardVisualKind.UnlockRelic,
                    null,
                    ProgressionRewardDescriptionUtility.Describe(reward),
                    reward));
                break;
        }
    }

    static void AddStatOrCompact(
        List<ProgressionRewardDisplayEntry> into,
        ProgressionRewardVisualCatalogSO catalog,
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        ProgressionRewardVisualKind kind,
        MainAttributeIconId iconId,
        string statLabel,
        int amount)
    {
        if (mode == ProgressionRewardExpandMode.Celebration)
        {
            into.Add(new ProgressionRewardDisplayEntry
            {
                kind = kind,
                style = ProgressionRewardDisplayStyle.Stat,
                mainAttributeIconId = iconId,
                statAmount = amount,
                statLabel = statLabel,
                sourceReward = reward,
            });
            return;
        }

        var icon = ResolveStatIcon(catalog, iconId);
        var placeholder = amount.ToString();
        var format = ResolveFormat(reward, trialRowFormatOverride);
        var text = FormatRowText(format, placeholder);
        into.Add(ProgressionRewardDisplayEntry.Compact(kind, icon, text, reward));
    }

    static void AddRelicEntries(
        List<ProgressionRewardDisplayEntry> into,
        ProgressionRewardVisualCatalogSO catalog,
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        RelicSO relic)
    {
        if (relic == null)
            return;

        if (mode == ProgressionRewardExpandMode.Celebration)
        {
            into.Add(BuildItemDetailEntry(
                ProgressionRewardVisualKind.StartingRelic,
                relic.icon,
                relic,
                null,
                null,
                FormatItemRowTitle(reward, GetRelicDisplayName(relic)),
                reward));
            return;
        }

        into.Add(ProgressionRewardDisplayEntry.Compact(
            ProgressionRewardVisualKind.StartingRelic,
            ResolveRelicIcon(catalog, relic),
            FormatItemRowTitle(reward, GetRelicDisplayName(relic)),
            reward));
    }

    static void AddRelicListEntries(
        List<ProgressionRewardDisplayEntry> into,
        ProgressionRewardVisualCatalogSO catalog,
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        List<RelicSO> relics)
    {
        if (relics == null || relics.Count == 0)
            return;

        if (mode == ProgressionRewardExpandMode.Celebration)
        {
            for (var i = 0; i < relics.Count; i++)
            {
                var relic = relics[i];
                if (relic == null)
                    continue;

                into.Add(BuildItemDetailEntry(
                    ProgressionRewardVisualKind.UnlockRelic,
                    relic.icon,
                    relic,
                    null,
                    null,
                    FormatItemRowTitle(reward, GetRelicDisplayName(relic)),
                    reward));
            }

            return;
        }

        if (relics.Count == 1 && relics[0] != null)
        {
            var relic = relics[0];
            into.Add(ProgressionRewardDisplayEntry.Compact(
                ProgressionRewardVisualKind.UnlockRelic,
                ResolveRelicIcon(catalog, relic),
                FormatItemRowTitle(reward, GetRelicDisplayName(relic)),
                reward));
            return;
        }

        var count = relics.Count.ToString();
        var format = ResolveFormat(reward, trialRowFormatOverride);
        into.Add(ProgressionRewardDisplayEntry.Compact(
            ProgressionRewardVisualKind.UnlockRelic,
            ResolveKindFallbackIcon(catalog, ProgressionRewardVisualKind.UnlockRelic),
            FormatRowText(format, count),
            reward));
    }

    static void AddGemListEntries(
        List<ProgressionRewardDisplayEntry> into,
        ProgressionRewardVisualCatalogSO catalog,
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        List<GemSO> gems)
    {
        if (gems == null || gems.Count == 0)
            return;

        if (mode == ProgressionRewardExpandMode.Celebration)
        {
            for (var i = 0; i < gems.Count; i++)
            {
                var gem = gems[i];
                if (gem == null)
                    continue;

                into.Add(BuildItemDetailEntry(
                    ProgressionRewardVisualKind.UnlockGem,
                    gem.icon,
                    null,
                    gem,
                    null,
                    FormatItemRowTitle(reward, gem.DisplayLabel),
                    reward));
            }

            return;
        }

        if (gems.Count == 1 && gems[0] != null)
        {
            var gem = gems[0];
            into.Add(ProgressionRewardDisplayEntry.Compact(
                ProgressionRewardVisualKind.UnlockGem,
                ResolveGemIcon(catalog, gem),
                FormatItemRowTitle(reward, gem.DisplayLabel),
                reward));
            return;
        }

        var count = gems.Count.ToString();
        var format = ResolveFormat(reward, trialRowFormatOverride);
        into.Add(ProgressionRewardDisplayEntry.Compact(
            ProgressionRewardVisualKind.UnlockGem,
            ResolveKindFallbackIcon(catalog, ProgressionRewardVisualKind.UnlockGem)
                ?? ResolveStatIcon(catalog, MainAttributeIconId.Power),
            FormatRowText(format, count),
            reward));
    }

    static void AddFaceEntries(
        List<ProgressionRewardDisplayEntry> into,
        ProgressionRewardVisualCatalogSO catalog,
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        List<DieFaceSO> faces)
    {
        if (faces == null || faces.Count == 0)
            return;

        if (mode == ProgressionRewardExpandMode.PerReward)
        {
            var count = CountNonNullFaces(faces).ToString();
            var format = ResolveFormat(reward, trialRowFormatOverride);
            into.Add(ProgressionRewardDisplayEntry.Compact(
                ProgressionRewardVisualKind.UnlockFace,
                ResolveKindFallbackIcon(catalog, ProgressionRewardVisualKind.UnlockFace)
                    ?? ResolveStatIcon(catalog, MainAttributeIconId.PhysicalDieUnlock),
                FormatRowText(format, count),
                reward));
            return;
        }

        var countsByType = new Dictionary<DieType, int>();
        for (var i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            if (face == null)
                continue;

            countsByType.TryGetValue(face.type, out var count);
            countsByType[face.type] = count + 1;
        }

        var types = new List<DieType>(countsByType.Keys);
        types.Sort();

        for (var t = 0; t < types.Count; t++)
        {
            var dieType = types[t];
            var count = countsByType[dieType];
            var icon = ResolveFaceTypeIcon(catalog, dieType);
            var text = FormatGroupedFaceRowText(reward, trialRowFormatOverride, dieType, count);
            into.Add(new ProgressionRewardDisplayEntry
            {
                kind = ProgressionRewardVisualKind.UnlockFace,
                style = ProgressionRewardDisplayStyle.Compact,
                icon = icon,
                text = text,
                faceUnlockDieType = dieType,
                sourceReward = reward,
            });
        }
    }

    public static string FormatDieTypeDisplayName(DieType dieType) => dieType switch
    {
        DieType.Damage => "Physical",
        DieType.Armor => "Armor",
        _ => dieType.ToString(),
    };

    static void AddDieEntries(
        List<ProgressionRewardDisplayEntry> into,
        ProgressionRewardVisualCatalogSO catalog,
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        DieAssetSO die)
    {
        if (die == null)
            return;

        if (mode == ProgressionRewardExpandMode.Celebration)
        {
            into.Add(BuildItemDetailEntry(
                ProgressionRewardVisualKind.AddStartingDie,
                die.uiIcon,
                null,
                null,
                die,
                FormatItemRowTitle(reward, GetDieDisplayName(die)),
                reward));
            return;
        }

        into.Add(ProgressionRewardDisplayEntry.Compact(
            ProgressionRewardVisualKind.AddStartingDie,
            ResolveDieIcon(catalog, die),
            FormatItemRowTitle(reward, GetDieDisplayName(die)),
            reward));
    }

    static void AddLegacyDiceEntries(
        List<ProgressionRewardDisplayEntry> into,
        ProgressionRewardVisualCatalogSO catalog,
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        ProgressionRewardExpandMode mode,
        List<DieAssetSO> dice)
    {
        if (dice == null || dice.Count == 0)
            return;

        if (mode == ProgressionRewardExpandMode.Celebration)
        {
            for (var i = 0; i < dice.Count; i++)
            {
                var die = dice[i];
                if (die == null)
                    continue;

                into.Add(BuildItemDetailEntry(
                    ProgressionRewardVisualKind.UnlockDiceLegacy,
                    die.uiIcon,
                    null,
                    null,
                    die,
                    FormatItemRowTitle(reward, GetDieDisplayName(die)),
                    reward));
            }

            return;
        }

        if (dice.Count == 1 && dice[0] != null)
        {
            var die = dice[0];
            into.Add(ProgressionRewardDisplayEntry.Compact(
                ProgressionRewardVisualKind.UnlockDiceLegacy,
                ResolveDieIcon(catalog, die),
                FormatItemRowTitle(reward, GetDieDisplayName(die)),
                reward));
            return;
        }

        var count = dice.Count.ToString();
        var format = ResolveFormat(reward, trialRowFormatOverride);
        into.Add(ProgressionRewardDisplayEntry.Compact(
            ProgressionRewardVisualKind.UnlockDiceLegacy,
            ResolveLegacyUnlockDiceAggregateIcon(catalog, dice),
            FormatRowText(format, count),
            reward));
    }

    static ProgressionRewardDisplayEntry BuildItemDetailEntry(
        ProgressionRewardVisualKind kind,
        Sprite icon,
        RelicSO relic,
        GemSO gem,
        DieAssetSO die,
        string text,
        ProgressionRewardBase sourceReward) =>
        new ProgressionRewardDisplayEntry
        {
            kind = kind,
            style = ProgressionRewardDisplayStyle.ItemDetail,
            icon = icon,
            text = text ?? string.Empty,
            relic = relic,
            gem = gem,
            die = die,
            sourceReward = sourceReward,
        };

    public static string FormatItemRowTitle(ProgressionRewardBase reward, string itemDisplayName)
    {
        if (reward == null)
            return itemDisplayName ?? string.Empty;

        var format = !string.IsNullOrWhiteSpace(reward.rowFormat)
            ? reward.rowFormat.Trim()
            : GetDefaultItemFormat(reward);

        return FormatRowText(format, itemDisplayName);
    }

    static string GetDefaultItemFormat(ProgressionRewardBase reward) => reward switch
    {
        ProgressionStartingRelicReward => "Start with {0}",
        ProgressionUnlockRelicsReward => "Unlock {0}",
        ProgressionUnlockGemsReward => "Unlock {0}",
        ProgressionAddStartingDieReward => "Add {0} to deck",
        ProgressionUnlockDiceReward => "Add {0} to deck",
        _ => "{0}",
    };

    static string FormatGroupedFaceRowText(
        ProgressionRewardBase reward,
        string trialRowFormatOverride,
        DieType dieType,
        int count)
    {
        if (!string.IsNullOrWhiteSpace(trialRowFormatOverride))
            return FormatRowText(trialRowFormatOverride.Trim(), count.ToString());

        if (!string.IsNullOrWhiteSpace(reward.rowFormat))
            return FormatRowText(reward.rowFormat.Trim(), count.ToString());

        var element = FormatDieTypeElementName(dieType);
        return count == 1
            ? $"Unlock 1 {element} face"
            : $"Unlock {count} {element} faces";
    }

    static string FormatDieTypeElementName(DieType dieType) => dieType switch
    {
        DieType.Damage => "physical",
        DieType.Armor => "armor",
        _ => dieType.ToString().ToLowerInvariant(),
    };

    static int CountNonNullFaces(List<DieFaceSO> faces)
    {
        var count = 0;
        for (var i = 0; i < faces.Count; i++)
        {
            if (faces[i] != null)
                count++;
        }

        return count;
    }

    static Sprite ResolveStatIcon(ProgressionRewardVisualCatalogSO catalog, MainAttributeIconId iconId)
    {
        var iconIndex = catalog != null ? catalog.IconIndex : null;
        if (iconIndex != null)
            return iconIndex.GetMainAttributeIcon(iconId);
        return GameIconCatalog.GetMainAttributeIcon(iconId);
    }

    static Sprite ResolveKindFallbackIcon(ProgressionRewardVisualCatalogSO catalog, ProgressionRewardVisualKind kind) =>
        catalog != null ? catalog.GetFallbackSprite(kind) : null;

    static Sprite ResolveRelicIcon(ProgressionRewardVisualCatalogSO catalog, RelicSO relic)
    {
        if (relic != null && relic.icon != null)
            return relic.icon;
        return ResolveKindFallbackIcon(catalog, ProgressionRewardVisualKind.UnlockRelic);
    }

    static Sprite ResolveGemIcon(ProgressionRewardVisualCatalogSO catalog, GemSO gem)
    {
        if (gem != null && gem.icon != null)
            return gem.icon;
        return ResolveKindFallbackIcon(catalog, ProgressionRewardVisualKind.UnlockGem)
               ?? ResolveStatIcon(catalog, MainAttributeIconId.Power);
    }

    static Sprite ResolveDieIcon(ProgressionRewardVisualCatalogSO catalog, DieAssetSO die)
    {
        if (die != null && die.uiIcon != null)
            return die.uiIcon;

        if (die != null)
        {
            var iconIndex = catalog != null ? catalog.IconIndex : null;
            var sprite = iconIndex != null
                ? iconIndex.GetDieUnlockMainAttributeIcon(die.dieType)
                : GameIconCatalog.GetDieUnlockMainAttributeIcon(die.dieType);
            if (sprite != null)
                return sprite;
        }

        return ResolveKindFallbackIcon(catalog, ProgressionRewardVisualKind.AddStartingDie)
               ?? ResolveStatIcon(catalog, MainAttributeIconId.PhysicalDieUnlock);
    }

    static Sprite ResolveLegacyUnlockDiceAggregateIcon(ProgressionRewardVisualCatalogSO catalog, List<DieAssetSO> dice)
    {
        if (dice != null)
        {
            for (var i = 0; i < dice.Count; i++)
            {
                var icon = ResolveDieIcon(catalog, dice[i]);
                if (icon != null)
                    return icon;
            }
        }

        return ResolveKindFallbackIcon(catalog, ProgressionRewardVisualKind.UnlockDiceLegacy)
               ?? ResolveStatIcon(catalog, MainAttributeIconId.PhysicalDieUnlock);
    }

    static Sprite ResolveFaceTypeIcon(ProgressionRewardVisualCatalogSO catalog, DieType dieType)
    {
        var iconIndex = catalog != null ? catalog.IconIndex : null;
        var sprite = iconIndex != null
            ? iconIndex.GetDieUnlockMainAttributeIcon(dieType)
            : GameIconCatalog.GetDieUnlockMainAttributeIcon(dieType);
        if (sprite != null)
            return sprite;

        return ResolveKindFallbackIcon(catalog, ProgressionRewardVisualKind.UnlockFace)
               ?? ResolveStatIcon(catalog, MainAttributeIconId.PhysicalDieUnlock);
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
        ProgressionUnlockFacesReward r => r.faces != null && CountNonNullFaces(r.faces) == 1
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
        _ => "{0}",
    };

    static string FormatRowText(string format, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(format))
            return placeholder ?? string.Empty;

        try
        {
            return string.Format(format, placeholder ?? string.Empty);
        }
        catch (FormatException)
        {
            return format.Replace("{0}", placeholder ?? string.Empty);
        }
    }

    static string GetRelicDisplayName(RelicSO relic) =>
        relic != null && !string.IsNullOrWhiteSpace(relic.title)
            ? relic.title.Trim()
            : relic != null ? relic.name : string.Empty;

    static string GetDieDisplayName(DieAssetSO die) =>
        die != null && !string.IsNullOrWhiteSpace(die.dieName)
            ? die.dieName.Trim()
            : die != null ? die.name : string.Empty;
}
