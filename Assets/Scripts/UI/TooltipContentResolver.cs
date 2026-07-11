using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Resolves tooltip copy for any hoverable <see cref="ScriptableObject"/> (die face, relic, gem, die, status effect):
/// one main entry plus secondary status/action explanation entries shown in the stacked secondary tooltip panel.
/// Secondary entries come from explicit data references (face actions, gem effect rows, relic action fields) merged
/// with statuses mentioned in the copy via TMP style tags (<see cref="StatusStyleTagScanner"/>).
/// </summary>
public static class TooltipContentResolver
{
    static readonly Dictionary<Type, FieldInfo[]> StatusFieldsByTypeCache = new Dictionary<Type, FieldInfo[]>();
    static readonly List<StatusEffectSO> StatusScratch = new List<StatusEffectSO>();

    /// <summary>
    /// Resolves main tooltip content and appends secondary entries to <paramref name="secondaryResults"/> (may be null).
    /// Returns false when the source has nothing to show in a generic tooltip.
    /// </summary>
    /// <param name="includeFaceHeader">
    /// When true, <see cref="DieFaceSO"/> sources include their name/description as the main tooltip (used where the
    /// hovered element does not already display them, e.g. the Die Tooltip grid and the face-replace screen).
    /// When false (default, e.g. face-picker cards that already show the name/description) the face main tooltip is suppressed.
    /// </param>
    public static bool TryResolve(ScriptableObject source, out TooltipContent main, List<TooltipContent> secondaryResults, bool includeFaceHeader = false)
    {
        main = default;
        if (source == null)
            return false;

        switch (source)
        {
            case GemSO gem:
                main = new TooltipContent(gem.DisplayLabel, gem.description);
                AppendGemSecondary(gem, main, secondaryResults);
                return true;

            case RelicSO relic:
                main = new TooltipContent(string.IsNullOrEmpty(relic.title) ? relic.name : relic.title, relic.description);
                AppendRelicSecondary(relic, main, secondaryResults);
                return true;

            case DieFaceSO face:
                // Only show the face name/description when the hovered element does not already display them.
                main = includeFaceHeader
                    ? new TooltipContent(face.Title, face.GetDescription(DieFaceDescriptionContext.ResolveActive()), face.uiTooltipBackground)
                    : default;
                AppendFaceSecondary(face, secondaryResults);
                return true;

            case DieAssetSO die:
                main = new TooltipContent(
                    string.IsNullOrEmpty(die.dieName) ? die.name : die.dieName,
                    $"Type: {die.dieType}",
                    die.uiTooltipBackground);
                return true;

            case StatusEffectSO status:
                main = new TooltipContent(
                    string.IsNullOrEmpty(status.effectName) ? status.name : status.effectName,
                    status.description);
                AppendScannedStatuses(main, secondaryResults, excluded: status);
                return true;

            case PlayerTrialSO:
                return false;

            default:
                main = new TooltipContent(source.name, string.Empty);
                return true;
        }
    }

    /// <summary>Secondary entries for free-form tooltip text (manual <c>SetContent</c> callers): style-tag scan only.</summary>
    public static void AppendSecondaryForText(string title, string description, List<TooltipContent> secondaryResults)
    {
        if (secondaryResults == null)
            return;

        StatusScratch.Clear();
        StatusStyleTagScanner.AppendStatusesMentionedIn(title, StatusScratch);
        StatusStyleTagScanner.AppendStatusesMentionedIn(description, StatusScratch);
        AppendStatusEntries(StatusScratch, secondaryResults, excluded: null, mainTitle: title);
    }

    /// <summary>Joins entries into one block: titles with " · ", descriptions with blank lines.</summary>
    public static TooltipContent JoinEntries(IReadOnlyList<TooltipContent> entries)
    {
        if (entries == null || entries.Count == 0)
            return default;

        var titles = new List<string>();
        var descriptions = new List<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!string.IsNullOrWhiteSpace(entry.Title))
                titles.Add(entry.Title.Trim());
            if (!string.IsNullOrWhiteSpace(entry.Description))
                descriptions.Add(entry.Description.Trim());
        }

        var title = titles.Count > 0 ? string.Join(" · ", titles) : "Effect";
        var description = descriptions.Count > 0 ? string.Join("\n\n", descriptions) : string.Empty;
        return new TooltipContent(title, description);
    }

    static void AppendFaceSecondary(DieFaceSO face, List<TooltipContent> secondaryResults)
    {
        if (secondaryResults == null)
            return;

        DieFaceGameIconOnlyTooltipText.TryBuildEntries(face, secondaryResults);

        // Still scan the face's own copy (not shown as a main tooltip) so statuses it mentions get explained.
        StatusScratch.Clear();
        StatusStyleTagScanner.AppendStatusesMentionedIn(face.Title, StatusScratch);
        StatusStyleTagScanner.AppendStatusesMentionedIn(face.GetDescription(DieFaceDescriptionContext.ResolveActive()), StatusScratch);
        AppendStatusEntries(StatusScratch, secondaryResults, excluded: null);
    }

    static void AppendGemSecondary(GemSO gem, TooltipContent main, List<TooltipContent> secondaryResults)
    {
        if (secondaryResults == null)
            return;

        StatusScratch.Clear();
        if (gem.effects != null)
        {
            for (var i = 0; i < gem.effects.Count; i++)
            {
                var entry = gem.effects[i];
                if (entry == null)
                    continue;
                CollectStatus(entry.statusDefinition, StatusScratch);
                CollectStatus(entry.burnDefinition, StatusScratch);
            }
        }

        AppendStatusEntries(StatusScratch, secondaryResults, excluded: null);
        AppendScannedStatuses(main, secondaryResults, excluded: null);
    }

    static void AppendRelicSecondary(RelicSO relic, TooltipContent main, List<TooltipContent> secondaryResults)
    {
        if (secondaryResults == null)
            return;

        StatusScratch.Clear();
        if (relic.actions != null)
        {
            for (var i = 0; i < relic.actions.Count; i++)
            {
                var action = relic.actions[i];
                if (action == null)
                    continue;

                var fields = GetStatusFields(action.GetType());
                for (var f = 0; f < fields.Length; f++)
                    CollectStatus(fields[f].GetValue(action) as StatusEffectSO, StatusScratch);
            }
        }

        AppendStatusEntries(StatusScratch, secondaryResults, excluded: null);
        AppendScannedStatuses(main, secondaryResults, excluded: null);
    }

    static void AppendScannedStatuses(TooltipContent main, List<TooltipContent> secondaryResults, StatusEffectSO excluded)
    {
        StatusScratch.Clear();
        StatusStyleTagScanner.AppendStatusesMentionedIn(main.Title, StatusScratch);
        StatusStyleTagScanner.AppendStatusesMentionedIn(main.Description, StatusScratch);
        AppendStatusEntries(StatusScratch, secondaryResults, excluded, main.Title);
    }

    static void AppendStatusEntries(
        List<StatusEffectSO> statuses,
        List<TooltipContent> secondaryResults,
        StatusEffectSO excluded,
        string mainTitle = null)
    {
        for (var i = 0; i < statuses.Count; i++)
        {
            var status = statuses[i];
            if (status == null || status == excluded)
                continue;

            var title = string.IsNullOrEmpty(status.effectName) ? status.name : status.effectName;
            if (TitlesMatchIgnoringRichText(title, mainTitle))
                continue;
            if (ContainsEntryWithTitle(secondaryResults, title))
                continue;

            secondaryResults.Add(new TooltipContent(title, status.description));
        }
    }

    static bool ContainsEntryWithTitle(List<TooltipContent> entries, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        for (var i = 0; i < entries.Count; i++)
        {
            if (TitlesMatchIgnoringRichText(entries[i].Title, title))
                return true;
        }

        return false;
    }

    static bool TitlesMatchIgnoringRichText(string a, string b)
    {
        var plainA = StatusStyleTagScanner.StripRichTextPublic(a);
        var plainB = StatusStyleTagScanner.StripRichTextPublic(b);
        if (plainA.Length == 0 || plainB.Length == 0)
            return false;
        return string.Equals(plainA, plainB, StringComparison.OrdinalIgnoreCase);
    }

    static void CollectStatus(StatusEffectSO status, List<StatusEffectSO> results)
    {
        if (status != null && !results.Contains(status))
            results.Add(status);
    }

    /// <summary>Serialized fields of type <see cref="StatusEffectSO"/> (or subclass) on relic action types.</summary>
    static FieldInfo[] GetStatusFields(Type type)
    {
        if (StatusFieldsByTypeCache.TryGetValue(type, out var cached))
            return cached;

        var results = new List<FieldInfo>();
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (var i = 0; i < fields.Length; i++)
            {
                if (typeof(StatusEffectSO).IsAssignableFrom(fields[i].FieldType))
                    results.Add(fields[i]);
            }
        }

        var array = results.ToArray();
        StatusFieldsByTypeCache[type] = array;
        return array;
    }
}
