/// <summary>
/// Optional hook for die-face actions whose tooltip / card text uses <c>{0}</c> in
/// <see cref="DieFaceSO"/> description lines. Implemented on the action that owns the calculation.
/// </summary>
public interface IFaceDescriptionPreviewValue
{
    bool TryGetDescriptionPreviewValue(DieFaceSO face, out int value);
}
