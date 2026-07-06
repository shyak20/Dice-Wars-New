using UnityEngine;

/// <summary>Tags flyout rows with the relic or gem that buffed this roll.</summary>
public static class RollBuffSourceIcon
{
    public static void TagFace(FaceResult face, RelicSO relic) => TagFace(face, relic?.icon);

    public static void TagFace(FaceResult face, GemSO gem) => TagFace(face, gem?.icon);

    public static void TagFace(FaceResult face, Sprite icon)
    {
        if (face == null || icon == null)
            return;

        face.BuffSourceIcon = icon;
    }

    public static FacePoolExtraContribution WithRelic(FacePoolExtraContribution contribution, RelicSO relic) =>
        WithSource(contribution, relic?.icon);

    public static FacePoolExtraContribution WithGem(FacePoolExtraContribution contribution, GemSO gem) =>
        WithSource(contribution, gem?.icon);

    public static FacePoolExtraContribution WithSource(FacePoolExtraContribution contribution, Sprite icon)
    {
        if (icon != null)
            contribution.SourceBuffIcon = icon;
        return contribution;
    }
}
