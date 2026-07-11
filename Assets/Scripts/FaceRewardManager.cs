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
    [Tooltip("After a face swap, seconds to show the slot’s new-face preview before hiding the picker and firing OnFaceRewardCompleted. Also used after no-match close (no preview). Skip-for-coins closes immediately.")]
    [SerializeField, Min(0f)] private float closeDelay = 2f;

    [Header("Skip face reward for coins")]
    [SerializeField, Min(0)] private int skipFaceRewardCoinsMin = 15;
    [SerializeField, Min(0)] private int skipFaceRewardCoinsMax = 30;
    [Tooltip("Turned on when the player presses Skip for coins. Assign a GameObject outside the Face Reward / Face Picker hierarchy so it stays visible after the picker closes.")]
    [SerializeField] private GameObject skipCoinsPressedFeedback;

    private DieFaceSO chosenFace;
    private DieAssetSO chosenDie;
    /// <summary>Win-stage only: same 3 faces until the row is consumed or a new victory rebuilds rewards (survives Back to win screen).</summary>
    private List<DieFaceSO> _winStageFaceOfferCache;
    private int _winStageSkipCoinsAmount;
    private int _pendingSkipCoinsAmount;

    private void Awake()
    {
        if (facePickerView == null || lootTable == null)
            Debug.LogError("FaceRewardManager: Missing references (FacePicker, loot table).");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (skipFaceRewardCoinsMin > skipFaceRewardCoinsMax)
            skipFaceRewardCoinsMax = skipFaceRewardCoinsMin;
    }
#endif

    void SetSkipCoinsPressedFeedbackActive(bool active)
    {
        if (skipCoinsPressedFeedback == null)
            return;

        skipCoinsPressedFeedback.SetActive(active);
    }

    public void StartFaceReward()
    {
        chosenFace = null;
        chosenDie = null;
        ReleaseWinStageFaceOfferCache();

        if (gemRewardView != null) gemRewardView.Hide();

        SetSkipCoinsPressedFeedbackActive(false);

        var preferredTypes = new HashSet<DieType>(
            PlayerDataContainer.Instance.RuntimeData.currentDeck.Select(d => d.dieType));
        var options = ProgressionLootRolls.RollFaces(lootTable, 3, preferredTypes);
        _pendingSkipCoinsAmount = RollSkipFaceRewardCoins();
        facePickerView.Show(
            options,
            OnFaceChosen,
            OnReplacementSlotChosen,
            onRewindToFacePick: RewindFacePickProgress,
            onSkipForCoins: OnSkipFaceRewardForCoins,
            skipCoinsPreviewAmount: _pendingSkipCoinsAmount);
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

        SetSkipCoinsPressedFeedbackActive(false);

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
            options = ProgressionLootRolls.RollFaces(lootTable, 3, preferredTypes);
            if (options == null || options.Count == 0)
            {
                Debug.LogError("FaceRewardManager.StartFaceRewardFromWinStage: loot roll returned no faces.");
                gameObject.SetActive(false);
                onBackToWin?.Invoke();
                return;
            }

            _winStageFaceOfferCache = options;
            _winStageSkipCoinsAmount = RollSkipFaceRewardCoins();
        }

        _pendingSkipCoinsAmount = _winStageSkipCoinsAmount;
        if (_pendingSkipCoinsAmount <= 0)
        {
            _winStageSkipCoinsAmount = RollSkipFaceRewardCoins();
            _pendingSkipCoinsAmount = _winStageSkipCoinsAmount;
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
            onRewindToFacePick: RewindFacePickProgress,
            onSkipForCoins: OnSkipFaceRewardForCoins,
            skipCoinsPreviewAmount: _pendingSkipCoinsAmount);
        gameObject.SetActive(true);
    }

    private void OnSkipFaceRewardForCoins()
    {
        chosenFace = null;
        chosenDie = null;

        var amount = _pendingSkipCoinsAmount;
        if (amount <= 0)
            amount = RollSkipFaceRewardCoins();

        var economy = RunEconomyManager.TryGetRuntime();
        if (economy == null)
        {
            Debug.LogError("FaceRewardManager.OnSkipFaceRewardForCoins: RunEconomyManager not found.");
            return;
        }

        economy.GrantGold(amount, null);
        SetSkipCoinsPressedFeedbackActive(true);
        CloseAfterSkipForCoins();
    }

    private int RollSkipFaceRewardCoins()
    {
        var min = Mathf.Min(skipFaceRewardCoinsMin, skipFaceRewardCoinsMax);
        var max = Mathf.Max(skipFaceRewardCoinsMin, skipFaceRewardCoinsMax);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private void CloseAfterSkipForCoins()
    {
        if (facePickerView != null)
            facePickerView.Hide();

        FaceRewardEvents.OnFaceRewardCompleted?.Invoke(null);
        ReleaseWinStageFaceOfferCache();
        gameObject.SetActive(false);
    }

    private void ReleaseWinStageFaceOfferCache()
    {
        _winStageFaceOfferCache = null;
        _winStageSkipCoinsAmount = 0;
    }

    private void RewindFacePickProgress()
    {
        chosenFace = null;
        chosenDie = null;
    }

    private void OnFaceChosen(DieFaceSO face)
    {
        chosenFace = face;
        var matchingDice = PlayerInventory.GetDiceEligibleForFaceReplacement(PlayerDataContainer.Instance.RuntimeData, face);
        if (matchingDice.Count == 0)
        {
            Debug.LogWarning("FaceRewardManager: No dice match the selected face element; closing reward flow.");
            StartCoroutine(CloseAfterDelay());
        }
    }

    private void OnReplacementSlotChosen(DieAssetSO die, int slotIndex, UIRewardSlot clickedSlot)
    {
        if (die == null || chosenFace == null)
            return;
        if (!SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slotIndex, chosenFace))
        {
            facePickerView?.NotifyFaceReplacementRuleError(die, slotIndex);
            return;
        }

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

        StartCoroutine(CloseAfterFaceSwap(clickedSlot));
    }

    private IEnumerator CloseAfterFaceSwap(UIRewardSlot clickedSlot)
    {
        if (clickedSlot != null && chosenFace != null)
            clickedSlot.ShowNewFacePickedPreview(chosenFace);

        if (closeDelay > 0f)
            yield return new WaitForSeconds(closeDelay);

        if (facePickerView != null)
            facePickerView.Hide();

        FaceRewardEvents.OnFaceRewardCompleted?.Invoke(chosenFace);
        ReleaseWinStageFaceOfferCache();
        gameObject.SetActive(false);
    }

    private IEnumerator CloseAfterDelay()
    {
        if (closeDelay > 0f)
            yield return new WaitForSeconds(closeDelay);

        if (facePickerView != null)
            facePickerView.Hide();

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
