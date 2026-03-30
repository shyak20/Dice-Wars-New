using System;
using UnityEngine;

public class FaceSlotSelectionView : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    [Header("Slot Buttons (6)")]
    [SerializeField] private FaceOptionUI[] slotButtons = new FaceOptionUI[6];

    [Header("New Face Preview")]
    [SerializeField] private FaceOptionUI newFacePreview;

    private Action<int> onSlotSelected;

    private void Awake()
    {
        if (panel == null) Debug.LogError("FaceSlotSelectionView: panel is not assigned!");
        if (newFacePreview == null) Debug.LogError("FaceSlotSelectionView: newFacePreview is not assigned!");

        if (slotButtons == null || slotButtons.Length != 6)
        {
            Debug.LogError("FaceSlotSelectionView: slotButtons must have exactly 6 entries!");
            return;
        }

        for (var i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null)
                Debug.LogError($"FaceSlotSelectionView: slotButtons[{i}] is not assigned!");
        }
    }

    public void Show(DieAssetSO die, DieFaceSO newFace, Action<int> callback)
    {
        if (die == null)
        {
            Debug.LogError("FaceSlotSelectionView: No die provided!");
            return;
        }

        if (die.faces == null || die.faces.Length != 6)
        {
            Debug.LogError($"FaceSlotSelectionView: Die '{die.dieName}' does not have exactly 6 faces!");
            return;
        }

        onSlotSelected = callback;

        newFacePreview.Setup(newFace, null);
        newFacePreview.SetInteractable(false);

        for (var i = 0; i < slotButtons.Length; i++)
        {
            var slotIndex = i;
            slotButtons[i].Setup(die.faces[i], _ => OnSlotClicked(slotIndex));
            slotButtons[i].SetInteractable(true);
        }

        panel.SetActive(true);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

    public void RefreshSlot(DieAssetSO die, int slot)
    {
        slotButtons[slot].Refresh(die.faces[slot]);
    }

    public void DisableInteraction()
    {
        for (var i = 0; i < slotButtons.Length; i++)
        {
            slotButtons[i].SetInteractable(false);
        }
    }

    private void OnSlotClicked(int slotIndex)
    {
        onSlotSelected?.Invoke(slotIndex);
    }
}
