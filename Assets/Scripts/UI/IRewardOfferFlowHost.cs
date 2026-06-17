/// <summary>
/// Host for <see cref="RunRewardOfferRow"/> face/gem flows (win popup, map treasure, etc.).
/// </summary>
public interface IRewardOfferFlowHost
{
    void NotifyFacePickerOpening();
    void NotifyFacePickerBackedOut();
    void NotifyFaceRewardRowRemoved();
}
