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
    [SerializeField] private List<DieFaceSO> faceOptions;

    private DieFaceSO chosenFace;
    private DieAssetSO chosenDie;

    private void Awake()
    {
        if (faceSelectionView == null) Debug.LogError("FaceRewardManager: faceSelectionView is not assigned!");
        if (diceSelectionView == null) Debug.LogError("FaceRewardManager: diceSelectionView is not assigned!");
        if (faceSlotSelectionView == null) Debug.LogError("FaceRewardManager: faceSlotSelectionView is not assigned!");
        if (faceOptions == null || faceOptions.Count < 3) Debug.LogError("FaceRewardManager: faceOptions must have at least 3 entries!");
    }

    public void StartFaceReward()
    {
        chosenFace = null;
        chosenDie = null;

        var options = PickRandomFaces(3);
        faceSelectionView.Show(options, OnFaceChosen);
        gameObject.SetActive(true);
    }

    private List<DieFaceSO> PickRandomFaces(int count)
    {
        var pool = new List<DieFaceSO>(faceOptions);
        var selected = new List<DieFaceSO>();

        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var index = Random.Range(0, pool.Count);
            selected.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return selected;
    }

    private void OnFaceChosen(DieFaceSO face)
    {
        chosenFace = face;

        var matchingDice = PlayerDataContainer.Instance.RuntimeData.currentDeck
            .Where(d => d.dieType == face.type)
            .ToList();

        if (matchingDice.Count == 0)
        {
            Debug.LogError($"FaceRewardManager: No dice of type {face.type} in player deck!");
            return;
        }

        if (matchingDice.Count == 1)
        {
            OnDieChosen(matchingDice[0]);
            return;
        }

        diceSelectionView.Show(matchingDice, OnDieChosen);
    }

    private void OnDieChosen(DieAssetSO die)
    {
        chosenDie = die;
        faceSlotSelectionView.Show(die, chosenFace, OnSlotChosen);
    }

    private void OnSlotChosen(int slotIndex)
    {
        var oldFace = chosenDie.faces[slotIndex];
        chosenDie.faces[slotIndex] = chosenFace;

        Debug.Log($"[FaceReward] Replaced '{oldFace.name}' with '{chosenFace.name}' on '{chosenDie.dieName}' slot {slotIndex}");

        gameObject.SetActive(false);
        FaceRewardEvents.OnFaceRewardCompleted?.Invoke(chosenFace);
    }
}
