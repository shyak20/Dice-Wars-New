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

    /// <summary>Shows the shrine UI. Returns false if the run is not in a valid map state (tile should stay unconsumed).</summary>
    public bool TryOpenPanel()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("MapShrineChoicePanel: RunManager missing.", this);
            return false;
        }

        if (!RunManager.Instance.UseMapBasedRun)
        {
            Debug.LogError("MapShrineChoicePanel: not in map-based run.", this);
            return false;
        }

        if (root == null)
            root = gameObject;

        // Parents must be active before children; otherwise the panel stays invisible even if root is enabled.
        ActivateSelfAndAncestors(root.transform);
        root.SetActive(true);
        return true;
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

    private void Close() => Hide();

    /// <summary>Closes the panel without applying a choice (e.g. map regenerated).</summary>
    public void Hide() => root.SetActive(false);

    private static void ActivateSelfAndAncestors(Transform t)
    {
        if (t == null) return;
        if (t.parent != null)
            ActivateSelfAndAncestors(t.parent);
        t.gameObject.SetActive(true);
    }
}
