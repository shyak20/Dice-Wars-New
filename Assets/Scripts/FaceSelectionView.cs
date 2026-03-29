using System;
using System.Collections.Generic;
using UnityEngine;

public class FaceSelectionView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject faceOptionPrefab;

    private Action<DieFaceSO> onSelected;

    private void Awake()
    {
        if (panel == null) Debug.LogError("FaceSelectionView: panel is not assigned!");
        if (buttonContainer == null) Debug.LogError("FaceSelectionView: buttonContainer is not assigned!");
        if (faceOptionPrefab == null) Debug.LogError("FaceSelectionView: faceOptionPrefab is not assigned!");
    }

    public void Show(List<DieFaceSO> options, Action<DieFaceSO> callback)
    {
        if (options == null || options.Count == 0)
        {
            Debug.LogError("FaceSelectionView: No face options provided!");
            return;
        }

        onSelected = callback;

        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        foreach (var face in options)
        {
            var btnObj = Instantiate(faceOptionPrefab, buttonContainer);
            var faceOption = btnObj.GetComponent<FaceOptionUI>();

            if (faceOption == null)
            {
                Debug.LogError("FaceSelectionView: faceOptionPrefab is missing FaceOptionUI component!");
                return;
            }

            faceOption.Setup(face, OnFaceClicked);
        }

        panel.SetActive(true);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

    private void OnFaceClicked(DieFaceSO face)
    {
        Hide();
        onSelected?.Invoke(face);
    }
}
