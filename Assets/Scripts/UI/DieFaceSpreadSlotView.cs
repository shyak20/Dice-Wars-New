using System;
using UnityEngine;

/// <summary>
/// One face on a <see cref="DieFaceSpreadView"/> spread prefab. Maps a child slot to <see cref="DieAssetSO.faces"/> via <see cref="faceIndex"/>.
/// </summary>
public sealed class DieFaceSpreadSlotView : MonoBehaviour
{
    [SerializeField, Range(0, 5)] private int faceIndex;
    [SerializeField] private UIRewardSlot rewardSlot;

    public int FaceIndex => faceIndex;
    public UIRewardSlot RewardSlot => rewardSlot;

    private void Awake()
    {
        if (rewardSlot == null)
            rewardSlot = GetComponent<UIRewardSlot>();
        if (rewardSlot == null)
            throw new InvalidOperationException($"DieFaceSpreadSlotView on '{name}': assign UIRewardSlot.");
        if (faceIndex < 0 || faceIndex > 5)
            throw new InvalidOperationException($"DieFaceSpreadSlotView on '{name}': faceIndex must be 0–5.");
    }

    public void Bind(DieFaceSO face, Action<int, UIRewardSlot> onClicked, DieTooltipOverlayUI faceHoverOverlay = null)
    {
        rewardSlot.Bind(face, _ =>
        {
            onClicked?.Invoke(faceIndex, rewardSlot);
        });
        rewardSlot.SetHoverRevealEnabled(true);
        rewardSlot.SetExternalStatusHoverTooltipEnabled(false);
        if (faceHoverOverlay != null && face != null)
            faceHoverOverlay.RegisterFaceSlotHover(rewardSlot, face);
        // Keep raycasts enabled so face hover tooltips work even on non-replaceable slots.
        rewardSlot.SetInteractable(true);
    }

    public void SetInteractable(bool interactable) => rewardSlot.SetInteractable(interactable);

    public void ShowNewFacePickedPreview(DieFaceSO newFace) => rewardSlot.ShowNewFacePickedPreview(newFace);

    public void HideNewFacePickedPreview() => rewardSlot.HideNewFacePickedPreview();
}
