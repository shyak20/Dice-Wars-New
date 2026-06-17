using System;
using System.Globalization;

/// <summary>Resolves <c>{0}</c> placeholders in <see cref="DieFaceSO"/> description lines from face actions.</summary>
public static class DieFaceDescriptionUtility
{
    public static bool ContainsPlaceholder(string text) =>
        !string.IsNullOrEmpty(text) && text.IndexOf("{0}", StringComparison.Ordinal) >= 0;

    public static string FormatDescription(DieFaceSO face, string rawDescription)
    {
        if (face == null || string.IsNullOrEmpty(rawDescription))
            return rawDescription ?? string.Empty;

        if (!ContainsPlaceholder(rawDescription))
            return rawDescription;

        if (!TryGetPreviewValue(face, out var value))
            return rawDescription;

        return string.Format(CultureInfo.InvariantCulture, rawDescription, value);
    }

    public static bool TryGetPreviewValue(DieFaceSO face, out int value)
    {
        value = 0;
        if (face?.actions == null)
            return false;

        for (var i = 0; i < face.actions.Count; i++)
        {
            var action = face.actions[i];
            if (action is IFaceDescriptionPreviewValue preview &&
                preview.TryGetDescriptionPreviewValue(face, out value))
                return true;
        }

        return false;
    }
}
