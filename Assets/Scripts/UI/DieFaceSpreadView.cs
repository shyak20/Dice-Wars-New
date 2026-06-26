using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Spread animation root for in-tray face replacement. Binds die faces to child <see cref="DieFaceSpreadSlotView"/> slots.
/// Rule-error UI lives on <see cref="DieTooltipOverlayUI"/> (same as the old interactive die tooltip).
/// </summary>
public sealed class DieFaceSpreadView : MonoBehaviour
{
    [SerializeField] private Animator spreadAnimator;
    [SerializeField] private string selectedBoolParameter = "Selected";
    [SerializeField] private DieFaceSpreadSlotView[] faceSlots;

    private int _selectedBoolParamHash;

    public DieAssetSO BoundDie { get; private set; }

    private void Awake()
    {
        if (spreadAnimator == null)
            spreadAnimator = GetComponent<Animator>();

        _selectedBoolParamHash = string.IsNullOrWhiteSpace(selectedBoolParameter)
            ? 0
            : Animator.StringToHash(selectedBoolParameter.Trim());

        if (faceSlots == null || faceSlots.Length == 0)
            faceSlots = GetComponentsInChildren<DieFaceSpreadSlotView>(true);

        if (faceSlots == null || faceSlots.Length == 0)
            throw new InvalidOperationException($"DieFaceSpreadView on '{name}': assign at least one DieFaceSpreadSlotView.");

        faceSlots = faceSlots.OrderBy(s => s.FaceIndex).ToArray();
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (spreadAnimator == null || spreadAnimator.runtimeAnimatorController == null || _selectedBoolParamHash == 0)
            return;

        spreadAnimator.SetBool(_selectedBoolParamHash, selected);
    }

    public void Bind(
        DieAssetSO die,
        DieFaceSO pendingRewardFace,
        Func<int, bool> slotAllowed,
        Action<int, DieFaceSO, UIRewardSlot> onSlotClicked,
        DieTooltipOverlayUI tooltipOverlay = null)
    {
        if (die == null)
            throw new ArgumentNullException(nameof(die));

        BoundDie = die;
        tooltipOverlay?.HideFaceReplacementRuleError();

        for (var i = 0; i < faceSlots.Length; i++)
        {
            var slotView = faceSlots[i];
            if (slotView == null)
                continue;

            var idx = slotView.FaceIndex;
            DieFaceSO face = null;
            if (die.faces != null && idx >= 0 && idx < die.faces.Length)
                face = die.faces[idx];

            slotView.HideNewFacePickedPreview();
            slotView.Bind(
                face,
                onClicked: (_, rewardSlot) =>
                {
                    if (slotAllowed != null && !slotAllowed(idx))
                    {
                        tooltipOverlay?.ShowFaceReplacementRuleError();
                        return;
                    }

                    var oldFace = face;
                    onSlotClicked?.Invoke(idx, oldFace, rewardSlot);
                },
                tooltipOverlay);
        }
    }

    public void SetAllSlotsInteractable(bool interactable)
    {
        for (var i = 0; i < faceSlots.Length; i++)
        {
            if (faceSlots[i] != null)
                faceSlots[i].SetInteractable(interactable);
        }
    }
}
