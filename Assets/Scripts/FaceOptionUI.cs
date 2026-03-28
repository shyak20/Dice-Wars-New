using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FaceOptionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button button;

    private DieFaceSO currentFace;
    private Action<DieFaceSO> onSelected;

    private void Awake()
    {
        if (titleText == null) Debug.LogError($"FaceOptionUI on '{gameObject.name}': titleText is not assigned!");
        if (descriptionText == null) Debug.LogError($"FaceOptionUI on '{gameObject.name}': descriptionText is not assigned!");
        if (button == null) Debug.LogError($"FaceOptionUI on '{gameObject.name}': button is not assigned!");
    }

    public void Setup(DieFaceSO face, Action<DieFaceSO> callback)
    {
        currentFace = face;
        onSelected = callback;

        titleText.text = face.Title;
        descriptionText.text = face.Description;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelected?.Invoke(currentFace));
    }

    public void Refresh(DieFaceSO face)
    {
        currentFace = face;
        titleText.text = face.Title;
        descriptionText.text = face.Description;
    }
}
