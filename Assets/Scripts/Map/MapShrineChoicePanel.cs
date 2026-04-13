using UnityEngine;
using UnityEngine.UI;

/// <summary>Map-only shrine: permanent run max-power bonus or heal. Wire buttons in the Map scene.</summary>
public sealed class MapShrineChoicePanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField, Min(1)] private int maxPowerBonus = 2;
    [SerializeField, Min(1)] private int healAmount = 20;
    [SerializeField] private Button maxPowerButton;
    [SerializeField] private Button healButton;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        root.SetActive(false);
        if (maxPowerButton != null)
            maxPowerButton.onClick.AddListener(OnMaxPowerChosen);
        if (healButton != null)
            healButton.onClick.AddListener(OnHealChosen);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public void OpenPanel()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("MapShrineChoicePanel: RunManager missing.", this);
            return;
        }

        if (!RunManager.Instance.UseMapBasedRun)
        {
            Debug.LogError("MapShrineChoicePanel: not in map-based run.", this);
            return;
        }

        root.SetActive(true);
    }

    private void OnMaxPowerChosen()
    {
        RunManager.Instance?.ApplyShrineMaxPowerBonus(maxPowerBonus);
        Close();
    }

    private void OnHealChosen()
    {
        RunManager.Instance?.ApplyShrineHeal(healAmount);
        Close();
    }

    private void Close() => root.SetActive(false);
}
