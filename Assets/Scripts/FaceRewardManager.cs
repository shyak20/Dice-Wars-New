using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Orchestrates reward flow: face picker → die disambiguation (if needed) → face swap overlay.
/// </summary>
public class FaceRewardManager : MonoBehaviour
{
    [Header("Views (Phases 2–4)")]
    [SerializeField] private FacePickerView facePickerView;
    [SerializeField] private DieDisambiguationView dieDisambiguationView;
    [SerializeField] private FaceSwapOverlayView faceSwapOverlayView;

    [Header("Data")]
    [SerializeField] private FaceLootTableSO lootTable;

    [Header("Timing")]
    [Tooltip("Seconds to wait after swap (or no-match close) before hiding overlay, firing OnFaceRewardCompleted, and deactivating. Set to 0 for immediate.")]
    [SerializeField, Min(0f)] private float closeDelay = 2f;

    private DieFaceSO chosenFace;
    private DieAssetSO chosenDie;

    private void Awake()
    {
        if (facePickerView == null || dieDisambiguationView == null || faceSwapOverlayView == null || lootTable == null)
            Debug.LogError("FaceRewardManager: Missing references (FacePicker, DieDisambiguation, FaceSwapOverlay, loot table).");
    }

    public void StartFaceReward()
    {
        chosenFace = null;
        chosenDie = null;

        if (dieDisambiguationView != null) dieDisambiguationView.Hide();
        if (faceSwapOverlayView != null) faceSwapOverlayView.Hide();

        var preferredTypes = new HashSet<DieType>(
            PlayerDataContainer.Instance.RuntimeData.currentDeck.Select(d => d.dieType));
        var options = lootTable.GetRandomRewards(3, preferredTypes);
        facePickerView.Show(options, OnFaceChosen);
        gameObject.SetActive(true);
    }

    /// <summary>Win-stage flow: picker with Back (return to win popup) and Skip (no new face).</summary>
    public void StartFaceRewardFromWinStage(Action onBackToWin, Action onSkippedFace)
    {
        chosenFace = null;
        chosenDie = null;

        if (dieDisambiguationView != null) dieDisambiguationView.Hide();
        if (faceSwapOverlayView != null) faceSwapOverlayView.Hide();

        if (lootTable == null || facePickerView == null || PlayerDataContainer.Instance == null)
        {
            Debug.LogError("FaceRewardManager.StartFaceRewardFromWinStage: missing loot table, picker, or player data.");
            onBackToWin?.Invoke();
            return;
        }

        var preferredTypes = new HashSet<DieType>(
            PlayerDataContainer.Instance.RuntimeData.currentDeck.Select(d => d.dieType));
        var options = lootTable.GetRandomRewards(3, preferredTypes);
        facePickerView.Show(
            options,
            OnFaceChosen,
            () =>
            {
                gameObject.SetActive(false);
                onBackToWin?.Invoke();
            },
            () =>
            {
                gameObject.SetActive(false);
                onSkippedFace?.Invoke();
            });
        gameObject.SetActive(true);
    }

    private void OnFaceChosen(DieFaceSO face)
    {
        chosenFace = face;
        var matchingDice = PlayerInventory.GetDiceMatchingFace(PlayerDataContainer.Instance.RuntimeData, face);

        if (matchingDice.Count == 0)
        {
            Debug.LogWarning(
                "FaceRewardManager: No dice in the current deck match this face's element; closing reward flow without swapping.");
            StartCoroutine(CloseAfterDelay());
            return;
        }
        if (matchingDice.Count == 1)
        {
            OnDieChosen(matchingDice[0]);
            return;
        }

        dieDisambiguationView.Show(matchingDice, OnDieChosen);
    }

    private void OnDieChosen(DieAssetSO die)
    {
        chosenDie = die;
        if (!faceSwapOverlayView.Show(die, chosenFace, OnSwapCommitted))
            StartCoroutine(CloseAfterDelay());
    }

    private void OnSwapCommitted()
    {
        StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        if (closeDelay > 0f)
            yield return new WaitForSeconds(closeDelay);

        if (faceSwapOverlayView != null)
            faceSwapOverlayView.Hide();

        FaceRewardEvents.OnFaceRewardCompleted?.Invoke(chosenFace);
        gameObject.SetActive(false);
    }
}
