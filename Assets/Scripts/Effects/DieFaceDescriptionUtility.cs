using System;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Resolves live values in <see cref="DieFaceSO"/> tooltip copy: <c>{0}</c> placeholders and
/// numeric <c>&lt;style=Attack&gt;</c> tags for physical attack faces.
/// </summary>
public static class DieFaceDescriptionUtility
{
    static readonly Regex StyledAttackNumberPattern = new Regex(
        @"(<style=Attack>)(\d+)(</style>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool ContainsPlaceholder(string text) =>
        !string.IsNullOrEmpty(text) && text.IndexOf("{0}", StringComparison.Ordinal) >= 0;

    public static string FormatDescription(DieFaceSO face, string rawDescription, DieFaceDescriptionContext context = null)
    {
        if (face == null || string.IsNullOrEmpty(rawDescription))
            return rawDescription ?? string.Empty;

        context ??= DieFaceDescriptionContext.ResolveActive();

        var formatted = rawDescription;
        if (ContainsPlaceholder(formatted) && TryGetPreviewValue(face, context, out var value))
            formatted = string.Format(CultureInfo.InvariantCulture, formatted, value);

        return ApplyPhysicalAttackPreviewToStyledAttackTags(face, formatted, context);
    }

    public static bool TryGetPreviewValue(DieFaceSO face, DieFaceDescriptionContext context, out int value)
    {
        value = 0;
        if (face?.actions != null)
        {
            for (var i = 0; i < face.actions.Count; i++)
            {
                var action = face.actions[i];
                if (action is IFaceDescriptionPreviewValue preview &&
                    preview.TryGetDescriptionPreviewValue(face, out value))
                    return true;
            }
        }

        return DieFacePhysicalAttackPreviewUtility.TryGetPerHitDamage(face, context, out value);
    }

    static string ApplyPhysicalAttackPreviewToStyledAttackTags(
        DieFaceSO face,
        string text,
        DieFaceDescriptionContext context)
    {
        if (string.IsNullOrEmpty(text) || face == null || face.type != DieType.Damage || face.damage <= 0)
            return text;

        if (!DieFacePhysicalAttackPreviewUtility.TryGetPerHitDamage(face, context, out var perHitDamage))
            return text;

        return StyledAttackNumberPattern.Replace(
            text,
            match =>
            {
                if (!int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var taggedValue))
                    return match.Value;

                if (taggedValue != face.damage)
                    return match.Value;

                var lineStart = text.LastIndexOf('\n', Math.Max(0, match.Index - 1));
                if (lineStart < 0)
                    lineStart = 0;
                else
                    lineStart++;

                var lineEnd = text.IndexOf('\n', match.Index);
                if (lineEnd < 0)
                    lineEnd = text.Length;

                var line = text.Substring(lineStart, lineEnd - lineStart);
                if (IsRateBasedAttackDescriptionLine(line))
                    return match.Value;

                return $"{match.Groups[1].Value}{perHitDamage}{match.Groups[3].Value}";
            });
    }

    static bool IsRateBasedAttackDescriptionLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        return line.IndexOf("additional", StringComparison.OrdinalIgnoreCase) >= 0
               || line.IndexOf(" for every", StringComparison.OrdinalIgnoreCase) >= 0
               || line.IndexOf(" per ", StringComparison.OrdinalIgnoreCase) >= 0
               || line.IndexOf("each ", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
