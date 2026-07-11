using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Detects status effects referenced in tooltip copy through TMP style tags (project convention:
/// status mentions are written as <c>&lt;style=Burn&gt;...&lt;/style&gt;</c> where the style name matches the
/// <see cref="StatusEffectSO"/> asset name). Matches only statuses registered in the active
/// <see cref="GameIconIndexSO"/>; non-status styles (Shield, Attack, Die1–6, rarities) simply never match.
/// </summary>
public static class StatusStyleTagScanner
{
    static readonly Regex StyleTagRegex = new Regex(@"<style=([^>/]+)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex RichTextTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);
    static readonly List<StatusEffectSO> RegisteredStatusesScratch = new List<StatusEffectSO>();

    /// <summary>
    /// Appends statuses whose style tag appears in <paramref name="text"/> to <paramref name="results"/> (no duplicates).
    /// </summary>
    public static void AppendStatusesMentionedIn(string text, List<StatusEffectSO> results)
    {
        if (string.IsNullOrWhiteSpace(text) || results == null)
            return;

        var matches = StyleTagRegex.Matches(text);
        if (matches.Count == 0)
            return;

        RegisteredStatusesScratch.Clear();
        GameIconCatalog.CollectRegisteredStatusEffects(RegisteredStatusesScratch);
        if (RegisteredStatusesScratch.Count == 0)
            return;

        foreach (Match match in matches)
        {
            var styleName = match.Groups[1].Value.Trim();
            if (styleName.Length == 0)
                continue;

            var status = FindStatusByStyleName(styleName);
            if (status != null && !results.Contains(status))
                results.Add(status);
        }
    }

    static StatusEffectSO FindStatusByStyleName(string styleName)
    {
        for (var i = 0; i < RegisteredStatusesScratch.Count; i++)
        {
            var status = RegisteredStatusesScratch[i];
            if (status == null)
                continue;

            if (string.Equals(status.name, styleName, System.StringComparison.OrdinalIgnoreCase))
                return status;

            var plainEffectName = StripRichText(status.effectName);
            if (plainEffectName.Length > 0
                && string.Equals(plainEffectName, styleName, System.StringComparison.OrdinalIgnoreCase))
                return status;
        }

        return null;
    }

    static string StripRichText(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : RichTextTagRegex.Replace(text, string.Empty).Trim();

    /// <summary>Strips TMP/HTML-like tags for title comparisons (dedupe across rich-text and plain labels).</summary>
    public static string StripRichTextPublic(string text) => StripRichText(text);
}
