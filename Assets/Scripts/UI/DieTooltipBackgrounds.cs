using UnityEngine.UI;

/// <summary>
/// Applies per-die / per-face tooltip frame sprites from <see cref="DieAssetSO"/> and <see cref="DieFaceSO"/>.
/// </summary>
public static class DieTooltipBackgrounds
{
    public static void ApplyDieTooltip(Image image, DieAssetSO die)
    {
        if (image == null) return;
        if (die == null)
        {
            Clear(image);
            return;
        }

        var s = die.uiTooltipBackground;
        image.sprite = s;
        image.enabled = s != null;
    }

    public static void ApplyFaceTooltip(Image image, DieFaceSO face)
    {
        if (image == null) return;
        if (face == null)
        {
            Clear(image);
            return;
        }

        var s = face.uiTooltipBackground;
        image.sprite = s;
        image.enabled = s != null;
    }

    public static void Clear(Image image)
    {
        if (image == null) return;
        image.sprite = null;
        image.enabled = false;
    }
}
