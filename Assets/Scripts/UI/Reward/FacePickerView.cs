using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 2: post-battle grid of 3 random, type-appropriate face rewards.
/// </summary>
public class FacePickerView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject rewardSlotPrefab;
    [Header("Win-stage flow (optional)")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button skipButton;

    private Action<DieFaceSO> _onFacePicked;
    private Action _onBack;
    private Action _onSkip;

    private void Awake()
    {
        if (panel == null) Debug.LogError("FacePickerView: assign panel.");
        if (slotContainer == null) Debug.LogError("FacePickerView: assign slotContainer.");
        if (rewardSlotPrefab == null) Debug.LogError("FacePickerView: assign rewardSlotPrefab (UIRewardSlot).");
    }

    public void Show(List<DieFaceSO> options, Action<DieFaceSO> callback, Action onBack = null, Action onSkip = null)
    {
        if (options == null || options.Count == 0)
        {
            Debug.LogError("FacePickerView: No face options.");
            return;
        }

        _onFacePicked = callback;
        _onBack = onBack;
        _onSkip = onSkip;

        ConfigureNavButtons();

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

    private void ConfigureNavButtons()
    {
        if (backButton != null)
        {
            backButton.gameObject.SetActive(_onBack != null);
            backButton.onClick.RemoveAllListeners();
            if (_onBack != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(_onSkip != null);
            skipButton.onClick.RemoveAllListeners();
            if (_onSkip != null)
                skipButton.onClick.AddListener(OnSkipClicked);
        }
    }

    private void OnBackClicked()
    {
        Hide();
        _onBack?.Invoke();
    }

    private void OnSkipClicked()
    {
        Hide();
        _onSkip?.Invoke();
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
