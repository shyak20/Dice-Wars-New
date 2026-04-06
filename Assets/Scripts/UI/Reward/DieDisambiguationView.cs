using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 3: choose which die receives the face when multiple dice match the reward element.
/// </summary>
public class DieDisambiguationView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform rowContainer;
    [SerializeField] private GameObject rowPrefab;

    private Action<DieAssetSO> _onDiePicked;

    private void Awake()
    {
        if (panel == null) Debug.LogError("DieDisambiguationView: assign panel.");
        if (rowContainer == null) Debug.LogError("DieDisambiguationView: assign rowContainer.");
        if (rowPrefab == null) Debug.LogError("DieDisambiguationView: assign rowPrefab (DieDisambiguationRowUI).");
    }

    public void Show(List<DieAssetSO> dice, Action<DieAssetSO> callback)
    {
        if (dice == null || dice.Count == 0)
        {
            Debug.LogError("DieDisambiguationView: No dice.");
            return;
        }

        _onDiePicked = callback;

        foreach (Transform c in rowContainer)
            Destroy(c.gameObject);

        foreach (var die in dice)
        {
            var go = Instantiate(rowPrefab, rowContainer);
            var row = go.GetComponent<DieDisambiguationRowUI>();
            if (row == null)
            {
                Debug.LogError("DieDisambiguationView: rowPrefab needs DieDisambiguationRowUI.");
                Destroy(go);
                return;
            }

            row.Bind(die, OnRowClicked);
        }

        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnRowClicked(DieAssetSO die)
    {
        Hide();
        _onDiePicked?.Invoke(die);
    }
}
