using System;
using AssetKits.ParticleImage;
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
    private ParticleImage[] _particleImages;

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

        if (faceSlots.Length > 1)
        {
            var needsSort = false;
            for (var i = 1; i < faceSlots.Length; i++)
            {
                if (faceSlots[i] == null || faceSlots[i - 1] == null
                    || faceSlots[i].FaceIndex < faceSlots[i - 1].FaceIndex)
                {
                    needsSort = true;
                    break;
                }
            }

            if (needsSort)
            {
                Array.Sort(faceSlots, (a, b) =>
                {
                    if (a == null && b == null) return 0;
                    if (a == null) return 1;
                    if (b == null) return -1;
                    return a.FaceIndex.CompareTo(b.FaceIndex);
                });
            }
        }

        _particleImages = GetComponentsInChildren<ParticleImage>(true);
        SetParticleEffectsEnabled(false);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        SetParticleEffectsEnabled(selected);

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
                });
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

    void SetParticleEffectsEnabled(bool enabled)
    {
        if (_particleImages == null)
            return;

        for (var i = 0; i < _particleImages.Length; i++)
        {
            if (_particleImages[i] != null)
                _particleImages[i].enabled = enabled;
        }
    }
}
