using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Map-only panel for <see cref="MapEventType.Unknown"/> tiles.
/// Current behavior: show panel and let the player continue.
/// </summary>
public sealed class MapUnknownEventPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button continueButton;

    private UnknownMapEventSO _currentEvent;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        root.SetActive(false);
        if (continueButton != null)
            continueButton.onClick.AddListener(Close);
    }

    /// <summary>
    /// Shows Unknown Event UI for the drawn event.
    /// Returns false if run state is invalid (tile should stay unconsumed).
    /// </summary>
    public bool TryOpenPanel(UnknownMapEventSO unknownEvent)
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("MapUnknownEventPanel: RunManager missing.", this);
            return false;
        }

        if (!RunManager.Instance.UseMapBasedRun)
        {
            Debug.LogError("MapUnknownEventPanel: not in map-based run.", this);
            return false;
        }

        _currentEvent = unknownEvent;
        if (_currentEvent != null)
            Debug.Log($"Unknown tile: {_currentEvent.DisplayLabel}" + (string.IsNullOrEmpty(_currentEvent.description) ? "" : $" — {_currentEvent.description}"));
        else
            Debug.Log("Unknown tile: no possibleUnknownEvents configured for this act.");

        if (root == null)
            root = gameObject;

        ActivateSelfAndAncestors(root.transform);
        root.SetActive(true);
        return true;
    }

    private void Close() => Hide();

    /// <summary>Closes the panel (e.g. map regenerated).</summary>
    public void Hide()
    {
        _currentEvent = null;
        if (root != null)
            root.SetActive(false);
    }

    private static void ActivateSelfAndAncestors(Transform t)
    {
        if (t == null) return;
        if (t.parent != null)
            ActivateSelfAndAncestors(t.parent);
        t.gameObject.SetActive(true);
    }
}
