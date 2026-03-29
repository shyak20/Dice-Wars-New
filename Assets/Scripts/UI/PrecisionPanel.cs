using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PrecisionPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private bool _wasShown;

    private void Awake()
    {
        if (panel == null || promptText == null || yesButton == null || noButton == null)
            Debug.LogError("PrecisionPanel: Missing references!");

        if (_wasShown)
        {
            return;
        }
        
        panel.SetActive(false);
    }

    public void Show(int powerAmount, Action<bool> onResult)
    {
        _wasShown = true;
        panel.SetActive(true);
        promptText.text = $"Add +{powerAmount} Power?";

        yesButton.onClick.RemoveAllListeners();
        noButton.onClick.RemoveAllListeners();

        yesButton.onClick.AddListener(() =>
        {
            panel.SetActive(false);
            onResult(true);
        });

        noButton.onClick.AddListener(() =>
        {
            panel.SetActive(false);
            onResult(false);
        });
    }
}
