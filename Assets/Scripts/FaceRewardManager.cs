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
    [SerializeField] private GemRewardView gemRewardView;

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

        if (gemRewardView != null) gemRewardView.Hide();

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

    /// <summary>Win-stage flow: picker with Back (return to win popup).</summary>
    public void StartFaceRewardFromWinStage(Action onBackToWin)
    {
        chosenFace = null;
        chosenDie = null;

        if (gemRewardView != null) gemRewardView.Hide();

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
            onBack: () =>
            {
                gameObject.SetActive(false);
                onBackToWin?.Invoke();
            },
            onRewindToFacePick: RewindFacePickProgress);
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

        FaceRewardEvents.OnFaceRewardCompleted?.Invoke(chosenFace);
        ReleaseWinStageFaceOfferCache();
        gameObject.SetActive(false);
    }

    /// <summary>Win-stage flow: choose a die socket for the collected gem.</summary>
    public void StartGemRewardFromWinStage(GemSO gem, Action onGemSocketed, Action onBackToWin = null)
    {
        if (gemRewardView == null)
        {
            Debug.LogError("FaceRewardManager.StartGemRewardFromWinStage: assign gemRewardView.");
            onBackToWin?.Invoke();
            return;
        }

        var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        var candidates = PlayerInventory.GetDiceWithEmptyGemSocket(data);
        if (candidates.Count <= 0)
        {
            Debug.LogWarning($"FaceRewardManager: cannot start gem reward for '{gem?.name}' — no free gem sockets.");
            onBackToWin?.Invoke();
            return;
        }

        if (facePickerView != null) facePickerView.Hide();
        gameObject.SetActive(true);
        gemRewardView.Show(
            gem,
            _ =>
            {
                gameObject.SetActive(false);
                onGemSocketed?.Invoke();
            },
            () =>
            {
                gameObject.SetActive(false);
                onBackToWin?.Invoke();
            });
    }
}
