using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FaceRewardManager : MonoBehaviour
{
    [Header("Views")]
    [SerializeField] private FaceSelectionView faceSelectionView;
    [SerializeField] private DiceSelectionView diceSelectionView;
    [SerializeField] private FaceSlotSelectionView faceSlotSelectionView;

    [Header("Data")]
    [SerializeField] private FaceLootTableSO lootTable;

    [Header("Timing")]
    [SerializeField] private float closeDelay = 2f;

    private DieFaceSO chosenFace;
    private DieAssetSO chosenDie;

    private void Awake()
    {
        if (faceSelectionView == null || diceSelectionView == null || faceSlotSelectionView == null || lootTable == null)
            Debug.LogError("FaceRewardManager: Missing references!");
    }

    public void StartFaceReward()
    {
        chosenFace = null;
        chosenDie = null;

        var preferredTypes = new HashSet<DieType>(
            PlayerDataContainer.Instance.RuntimeData.currentDeck.Select(d => d.dieType));
        var options = lootTable.GetRandomRewards(3, preferredTypes);
        faceSelectionView.Show(options, OnFaceChosen);
        gameObject.SetActive(true);
    }

    private void OnFaceChosen(DieFaceSO face)
    {
        chosenFace = face;
        // Logic remains compatible as it checks for type equality
        var matchingDice = PlayerDataContainer.Instance.RuntimeData.currentDeck
            .Where(d => d.dieType == face.type)
            .ToList();

        if (matchingDice.Count == 0) return;
        if (matchingDice.Count == 1) { OnDieChosen(matchingDice[0]); return; }
        diceSelectionView.Show(matchingDice, OnDieChosen);
    }

    private void OnDieChosen(DieAssetSO die)
    {
        chosenDie = die;
        faceSlotSelectionView.Show(die, chosenFace, OnSlotChosen);
    }

    private void OnSlotChosen(int slotIndex)
    {
        chosenDie.faces[slotIndex] = chosenFace;
        faceSlotSelectionView.RefreshSlot(chosenDie, slotIndex);
        faceSlotSelectionView.DisableInteraction();
        StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        faceSlotSelectionView.Hide();
        gameObject.SetActive(false);
        FaceRewardEvents.OnFaceRewardCompleted?.Invoke(chosenFace);
    }
}