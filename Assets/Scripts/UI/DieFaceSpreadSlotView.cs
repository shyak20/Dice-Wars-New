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

    public void Bind(DieFaceSO face, Action<int, UIRewardSlot> onClicked)
    {
        rewardSlot.Bind(face, _ =>
        {
            onClicked?.Invoke(faceIndex, rewardSlot);
        });
        rewardSlot.SetHoverRevealEnabled(true);
        // Face hover tooltips come from the shared HoverTooltipTargetUI wired in UIRewardSlot.Bind.
        rewardSlot.EnsureStandaloneHoverReveal();
        // Face-replace slots only show the icon, so include the face name/description in the tooltip.
        rewardSlot.SetFaceTooltipIncludesHeader(true);
        // Keep raycasts enabled so face hover tooltips work even on non-replaceable slots.
        rewardSlot.SetInteractable(true);
    }

    public void SetInteractable(bool interactable) => rewardSlot.SetInteractable(interactable);

    public void ShowNewFacePickedPreview(DieFaceSO newFace) => rewardSlot.ShowNewFacePickedPreview(newFace);

    public void HideNewFacePickedPreview() => rewardSlot.HideNewFacePickedPreview();
}
