using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Phase 4: pick which face slot to replace; hover shows that slot's current face title/description; etching FX on confirm.
/// Uses <see cref="UIRewardSlot"/> for the new face and each current-face option (same prefab as the picker).
/// </summary>
public class FaceSwapOverlayView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private UIRewardSlot newFacePreview;
    [SerializeField] private UIRewardSlot[] slotSlots = new UIRewardSlot[6];
    [SerializeField] private TMP_Text hoverFaceTitleText;
    [SerializeField] private TMP_Text hoverFaceDescriptionText;
    [SerializeField] private GameObject etchingParticlePrefab;
    [SerializeField] private AudioClip etchingSound;

    private DieAssetSO _die;
    private DieFaceSO _newFace;
    private Action _onCommitted;

    private void Awake()
    {
        if (panel == null) Debug.LogError("FaceSwapOverlayView: assign panel.");
        if (newFacePreview == null) Debug.LogError("FaceSwapOverlayView: assign newFacePreview (UIRewardSlot).");
        if (slotSlots == null || slotSlots.Length != 6)
            Debug.LogError("FaceSwapOverlayView: slotSlots must have 6 UIRewardSlot entries.");
    }

    /// <returns>False if the die/face was invalid and the overlay did not open.</returns>
    public bool Show(DieAssetSO die, DieFaceSO newFace, Action onCommitted)
    {
        if (die == null || die.faces == null || die.faces.Length != 6)
        {
            Debug.LogError("FaceSwapOverlayView: invalid die.");
            return false;
        }

        if (newFace != null && !die.CanAttachFace(newFace))
        {
            Debug.LogError("FaceSwapOverlayView: face element does not match die.");
            return false;
        }

        _die = die;
        _newFace = newFace;
        _onCommitted = onCommitted;

        newFacePreview.Bind(newFace, null);
        newFacePreview.SetInteractable(false);

        for (var i = 0; i < slotSlots.Length; i++)
        {
            if (slotSlots[i] == null) continue;
            var idx = i;
            slotSlots[i].Bind(die.faces[i], _ => OnSlotClicked(idx));
            slotSlots[i].SetInteractable(true);
            RegisterHover(slotSlots[i], idx);
        }

        ClearHoverPreview();
        panel.SetActive(true);
        return true;
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void DisableInteraction()
    {
        for (var i = 0; i < slotSlots.Length; i++)
            slotSlots[i]?.SetInteractable(false);
    }

    private void RegisterHover(UIRewardSlot slot, int slotIndex)
    {
        var go = slot != null ? slot.GetHoverTarget() : null;
        if (go == null) return;

        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        et.triggers.Clear();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => UpdateHoverFaceTexts(slotIndex));
        et.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ClearHoverPreview());
        et.triggers.Add(exit);
    }

    private void UpdateHoverFaceTexts(int slotIndex)
    {
        if (_die == null || _die.faces == null) return;
        var face = slotIndex >= 0 && slotIndex < _die.faces.Length ? _die.faces[slotIndex] : null;

        if (hoverFaceTitleText != null)
            hoverFaceTitleText.text = face != null ? face.Title : "";

        if (hoverFaceDescriptionText != null)
            hoverFaceDescriptionText.text = face != null ? face.Description : "";
    }

    private void ClearHoverPreview()
    {
        if (hoverFaceTitleText != null) hoverFaceTitleText.text = "";
        if (hoverFaceDescriptionText != null) hoverFaceDescriptionText.text = "";
    }

    private void OnSlotClicked(int slotIndex)
    {
        DisableInteraction();

        try
        {
            _die.SwapFace(slotIndex, _newFace);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return;
        }

        if (etchingParticlePrefab != null)
        {
            var fx = Instantiate(etchingParticlePrefab, transform.position, Quaternion.identity, transform);
            Destroy(fx, 4f);
        }

        if (etchingSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(etchingSound, Camera.main.transform.position, 0.85f);

        _onCommitted?.Invoke();
        Hide();
    }
}
