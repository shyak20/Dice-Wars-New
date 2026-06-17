public enum ProgressionUnlockedContentKind
{
    Face,
    Relic,
}

/// <summary>One unlock entry (face or relic) for <see cref="ProgressionUnlockedContentPopupView"/>.</summary>
public readonly struct ProgressionUnlockedContentItem
{
    public ProgressionUnlockedContentKind Kind { get; }
    public DieFaceSO Face { get; }
    public RelicSO Relic { get; }

    ProgressionUnlockedContentItem(ProgressionUnlockedContentKind kind, DieFaceSO face, RelicSO relic)
    {
        Kind = kind;
        Face = face;
        Relic = relic;
    }

    public static ProgressionUnlockedContentItem FromFace(DieFaceSO face) =>
        face == null ? default : new ProgressionUnlockedContentItem(ProgressionUnlockedContentKind.Face, face, null);

    public static ProgressionUnlockedContentItem FromRelic(RelicSO relic) =>
        relic == null ? default : new ProgressionUnlockedContentItem(ProgressionUnlockedContentKind.Relic, null, relic);

    public bool IsValid =>
        Kind == ProgressionUnlockedContentKind.Face ? Face != null : Kind == ProgressionUnlockedContentKind.Relic && Relic != null;
}
