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
    [SerializeField] private float closeDelay = 2f;

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
        faceSwapOverlayView.Show(die, chosenFace, OnSwapCommitted);
    }

    private void OnSwapCommitted()
    {
        StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        faceSwapOverlayView.Hide();
        gameObject.SetActive(false);
        FaceRewardEvents.OnFaceRewardCompleted?.Invoke(chosenFace);
    }
}
