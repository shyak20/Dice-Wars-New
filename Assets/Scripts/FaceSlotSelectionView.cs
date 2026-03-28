using System;
using UnityEngine;
using TMPro;

public class FaceSlotSelectionView : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    [Header("Slot Buttons (6)")]
    [SerializeField] private FaceOptionUI[] slotButtons = new FaceOptionUI[6];

    [Header("New Face Preview")]
    [SerializeField] private TMP_Text newFaceTitleText;
    [SerializeField] private TMP_Text newFaceDescriptionText;

    private Action<int> onSlotSelected;

    private void Awake()
    {
        if (panel == null) Debug.LogError("FaceSlotSelectionView: panel is not assigned!");
        if (newFaceTitleText == null) Debug.LogError("FaceSlotSelectionView: newFaceTitleText is not assigned!");
        if (newFaceDescriptionText == null) Debug.LogError("FaceSlotSelectionView: newFaceDescriptionText is not assigned!");

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

        newFaceTitleText.text = newFace.Title;
        newFaceDescriptionText.text = newFace.Description;

        for (var i = 0; i < slotButtons.Length; i++)
        {
            var slotIndex = i;
            slotButtons[i].Setup(die.faces[i], _ => OnSlotClicked(slotIndex));
        }

        panel.SetActive(true);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

    private void OnSlotClicked(int slotIndex)
    {
        Hide();
        onSlotSelected?.Invoke(slotIndex);
    }
}
