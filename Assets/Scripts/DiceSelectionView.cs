using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DiceSelectionView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject simpleButtonPrefab;

    private Action<DieAssetSO> onSelected;

    private void Awake()
    {
        if (panel == null) Debug.LogError("DiceSelectionView: panel is not assigned!");
        if (buttonContainer == null) Debug.LogError("DiceSelectionView: buttonContainer is not assigned!");
        if (simpleButtonPrefab == null) Debug.LogError("DiceSelectionView: simpleButtonPrefab is not assigned!");
    }

    public void Show(List<DieAssetSO> dice, Action<DieAssetSO> callback)
    {
        if (dice == null || dice.Count == 0)
        {
            Debug.LogError("DiceSelectionView: No dice provided!");
            return;
        }

        onSelected = callback;

        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        for (var i = 0; i < dice.Count; i++)
        {
            var die = dice[i];
            var btnObj = Instantiate(simpleButtonPrefab, buttonContainer);

            var label = btnObj.GetComponentInChildren<TMP_Text>();
            if (label == null)
            {
                Debug.LogError("DiceSelectionView: simpleButtonPrefab is missing TMP_Text component!");
                return;
            }

            label.text = $"Dice {i + 1} ({die.dieName})";

            var button = btnObj.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("DiceSelectionView: simpleButtonPrefab is missing Button component!");
                return;
            }

            var capturedDie = die;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnDieClicked(capturedDie));
        }

        panel.SetActive(true);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

    private void OnDieClicked(DieAssetSO die)
    {
        Hide();
        onSelected?.Invoke(die);
    }
}
