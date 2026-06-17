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

    [Header("Button labels (assign TMP child on each button so text updates per prompt)")]
    [SerializeField] private TMP_Text yesButtonCaption;
    [SerializeField] private TMP_Text noButtonCaption;

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
        Show(powerAmount, PrecisionPromptPresentation.Default, onResult);
    }

    public void Show(int powerAmount, PrecisionPromptPresentation presentation, Action<bool> onResult)
    {
        _wasShown = true;
        panel.SetActive(true);

        switch (presentation)
        {
            case PrecisionPromptPresentation.AddPowerAbility:
                promptText.text =
                    $"This ability can add +{powerAmount} Power.\nChoose whether to use it.";
                ApplyButtonCaptions("Use ability", "Ignore");
                break;
            default:
                promptText.text = $"Add +{powerAmount} Power?";
                ApplyButtonCaptions("Yes", "No");
                break;
        }

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

    private void ApplyButtonCaptions(string yes, string no)
    {
        if (yesButtonCaption != null)
            yesButtonCaption.text = yes;
        if (noButtonCaption != null)
            noButtonCaption.text = no;
    }
}
