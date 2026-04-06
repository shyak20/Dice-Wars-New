using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 2: post-battle grid of 3 random, type-appropriate face rewards.
/// </summary>
public class FacePickerView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject rewardSlotPrefab;

    private Action<DieFaceSO> _onFacePicked;

    private void Awake()
    {
        if (panel == null) Debug.LogError("FacePickerView: assign panel.");
        if (slotContainer == null) Debug.LogError("FacePickerView: assign slotContainer.");
        if (rewardSlotPrefab == null) Debug.LogError("FacePickerView: assign rewardSlotPrefab (UIRewardSlot).");
    }

    public void Show(List<DieFaceSO> options, Action<DieFaceSO> callback)
    {
        if (options == null || options.Count == 0)
        {
            Debug.LogError("FacePickerView: No face options.");
            return;
        }

        _onFacePicked = callback;

        foreach (Transform c in slotContainer)
            Destroy(c.gameObject);

        foreach (var face in options)
        {
            var go = Instantiate(rewardSlotPrefab, slotContainer);
            var slot = go.GetComponent<UIRewardSlot>();
            if (slot == null)
            {
                Debug.LogError("FacePickerView: rewardSlotPrefab needs UIRewardSlot.");
                Destroy(go);
                return;
            }

            slot.Bind(face, OnSlotClicked);
        }

        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnSlotClicked(DieFaceSO face)
    {
        Hide();
        _onFacePicked?.Invoke(face);
    }
}
