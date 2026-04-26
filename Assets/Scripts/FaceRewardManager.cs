using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Orchestrates reward flow: single-screen picker + deck tooltip-based face replacement.
/// </summary>
public class FaceRewardManager : MonoBehaviour
{
    [Header("Views")]
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
    /// <summary>Win-stage only: same 3 faces until the row is consumed or a new victory rebuilds rewards (survives Back to win screen).</summary>
    private List<DieFaceSO> _winStageFaceOfferCache;

    private void Awake()
    {
        if (facePickerView == null || lootTable == null)
            Debug.LogError("FaceRewardManager: Missing references (FacePicker, loot table).");
    }

    public void StartFaceReward()
    {
        chosenFace = null;
        chosenDie = null;
        ReleaseWinStageFaceOfferCache();

        if (dieDisambiguationView != null) dieDisambiguationView.Hide();
        if (faceSwapOverlayView != null) faceSwapOverlayView.Hide();

        var preferredTypes = new HashSet<DieType>(
            PlayerDataContainer.Instance.RuntimeData.currentDeck.Select(d => d.dieType));
        var options = lootTable.GetRandomRewards(3, preferredTypes);
        facePickerView.Show(options, OnFaceChosen, OnReplacementSlotChosen, onRewindToFacePick: RewindFacePickProgress);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Call from <see cref="WinStageFlowController"/> when reward rows are rebuilt for a new victory so the next face offer is a fresh roll.
    /// </summary>
    public void OnWinStageRewardsLayoutRebuilt()
    {
        ReleaseWinStageFaceOfferCache();
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
        var options = _winStageFaceOfferCache;
        if (options == null || options.Count == 0)
        {
            options = lootTable.GetRandomRewards(3, preferredTypes);
            if (options == null || options.Count == 0)
            {
                Debug.LogError("FaceRewardManager.StartFaceRewardFromWinStage: loot roll returned no faces.");
                gameObject.SetActive(false);
                onBackToWin?.Invoke();
                return;
            }

            _winStageFaceOfferCache = options;
        }

        facePickerView.Show(
            options,
            OnFaceChosen,
            OnReplacementSlotChosen,
            () =>
            {
                gameObject.SetActive(false);
                onBackToWin?.Invoke();
            },
            () =>
            {
                ReleaseWinStageFaceOfferCache();
                gameObject.SetActive(false);
                onSkippedFace?.Invoke();
            },
            RewindFacePickProgress);
        gameObject.SetActive(true);
    }

    private void ReleaseWinStageFaceOfferCache()
    {
        _winStageFaceOfferCache = null;
    }

    private void RewindFacePickProgress()
    {
        chosenFace = null;
        chosenDie = null;
    }

    private void OnFaceChosen(DieFaceSO face)
    {
        chosenFace = face;
        var matchingDice = PlayerInventory.GetDiceMatchingFace(PlayerDataContainer.Instance.RuntimeData, face);
        if (matchingDice.Count == 0)
        {
            Debug.LogWarning("FaceRewardManager: No dice match the selected face element; closing reward flow.");
            StartCoroutine(CloseAfterDelay());
        }
    }

    private void OnReplacementSlotChosen(DieAssetSO die, int slotIndex)
    {
        if (die == null || chosenFace == null)
            return;
        chosenDie = die;

        try
        {
            chosenDie.SwapFace(slotIndex, chosenFace);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return;
        }

        StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        if (facePickerView != null)
            facePickerView.Hide();

        if (closeDelay > 0f)
            yield return new WaitForSeconds(closeDelay);

        if (faceSwapOverlayView != null)
            faceSwapOverlayView.Hide();

        FaceRewardEvents.OnFaceRewardCompleted?.Invoke(chosenFace);
        ReleaseWinStageFaceOfferCache();
        gameObject.SetActive(false);
    }
}
