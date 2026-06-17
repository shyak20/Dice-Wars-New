using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Abandons the current map-based run (HP → 0, defeat screen). Only interactable on map / fight / shop during an active run.
/// </summary>
public sealed class UIAbandonRunButton : MonoBehaviour
{
    [SerializeField] private Button abandonButton;
    [SerializeField] private OptionsMenuUI optionsMenu;
    [Tooltip("Optional root hidden with the abandon control when unavailable.")]
    [SerializeField] private GameObject abandonControlRoot;

    void Awake()
    {
        if (abandonButton == null)
            abandonButton = GetComponent<Button>();

        if (abandonButton == null)
        {
            Debug.LogError($"UIAbandonRunButton on '{name}': assign abandonButton.", this);
            return;
        }

        if (optionsMenu == null)
            optionsMenu = GetComponentInParent<OptionsMenuUI>(true);

        abandonButton.onClick.AddListener(OnAbandonClicked);
    }

    void OnEnable() => RefreshAvailability();

    void OnDestroy()
    {
        if (abandonButton != null)
            abandonButton.onClick.RemoveListener(OnAbandonClicked);
    }

    public void RefreshAvailability()
    {
        var run = RunManager.Instance;
        var available = run != null && run.IsAbandonRunAvailableInCurrentScene();

        if (abandonButton != null)
            abandonButton.interactable = available;

        if (abandonControlRoot != null)
            abandonControlRoot.SetActive(available);
    }

    void OnAbandonClicked()
    {
        var run = RunManager.Instance;
        if (run == null || !run.IsAbandonRunAvailableInCurrentScene())
        {
            RefreshAvailability();
            return;
        }

        optionsMenu?.CloseSettings();
        run.ForceAbandonRunToDefeat();
    }
}
