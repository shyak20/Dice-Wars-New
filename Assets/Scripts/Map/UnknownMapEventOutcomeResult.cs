using UnityEngine;

public enum UnknownMapEventOutcomeResultKind
{
    RelicGranted,
    CurseFaceAdded,
}

/// <summary>Payload for <see cref="MapUnknownEventOutcomeResultView"/> after an unknown event option resolves.</summary>
public sealed class UnknownMapEventOutcomeResult
{
    public UnknownMapEventOutcomeResultKind Kind;
    public string Title;
    public string Body;
    public Sprite PrimaryIcon;
    public Sprite SecondaryIcon;
    public DieAssetSO AffectedDie;
    /// <summary>Fallback when <see cref="AffectedDie"/> is missing — index in <see cref="PlayerDataSO.currentDeck"/>.</summary>
    public int AffectedDieDeckIndex = -1;
    public DieFaceSO Face;
    public RelicSO Relic;
    public int FaceSlotIndex = -1;
}

/// <summary>Outcomes write here during <see cref="UnknownMapEventOutcomeBase.Execute"/>; the map panel reads it when <see cref="UnknownMapEventOptionEntry.showOutcomeResultScreen"/> is set.</summary>
public sealed class UnknownMapEventOutcomeResultRecorder
{
    UnknownMapEventOutcomeResult _result;

    public bool HasResult => _result != null;

    public UnknownMapEventOutcomeResult PeekResult() => _result;

    public UnknownMapEventOutcomeResult ConsumeResult()
    {
        var r = _result;
        _result = null;
        return r;
    }

    public void SetRelicGranted(RelicSO relic)
    {
        if (relic == null)
            return;

        _result = new UnknownMapEventOutcomeResult
        {
            Kind = UnknownMapEventOutcomeResultKind.RelicGranted,
            Title = string.IsNullOrWhiteSpace(relic.title) ? relic.name : relic.title.Trim(),
            Body = relic.description ?? string.Empty,
            PrimaryIcon = relic.icon,
            Relic = relic,
        };
    }

    public void SetCurseFaceAdded(DieAssetSO die, DieFaceSO curseFace, int slotIndex, int deckIndex = -1)
    {
        if (die == null || curseFace == null)
            return;

        var dieLabel = string.IsNullOrWhiteSpace(die.dieName) ? die.name : die.dieName.Trim();
        var curseTitle = curseFace.Title;
        _result = new UnknownMapEventOutcomeResult
        {
            Kind = UnknownMapEventOutcomeResultKind.CurseFaceAdded,
            Title = $"{dieLabel} — {curseTitle}",
            Body = curseFace.Description,
            PrimaryIcon = die.uiIcon,
            SecondaryIcon = ResolveFaceIcon(curseFace),
            AffectedDie = die,
            AffectedDieDeckIndex = deckIndex,
            Face = curseFace,
            FaceSlotIndex = slotIndex,
        };
    }

    static Sprite ResolveFaceIcon(DieFaceSO face)
    {
        if (face == null)
            return null;
        if (face.uiIcon != null)
            return face.uiIcon;
        return GameIconCatalog.GetElementIcon(face.type);
    }
}
