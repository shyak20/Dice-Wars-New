using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Generic two-button confirmation overlay. Place one per scene (or prefab instance under the scene canvas).
/// <see cref="Instance"/> resolves an enabled dialog in <see cref="SceneManager.GetActiveScene"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConfirmationDialog : MonoBehaviour
{
    static readonly List<ConfirmationDialog> Registry = new List<ConfirmationDialog>();

    /// <summary>Enabled dialog in the active scene with a valid panel, or null.</summary>
    public static ConfirmationDialog Instance
    {
        get
        {
            CleanupDestroyedEntries();
            var activeScene = SceneManager.GetActiveScene();
            for (var i = 0; i < Registry.Count; i++)
            {
                var d = Registry[i];
                if (d == null || !d.isActiveAndEnabled)
                    continue;
                if (d.gameObject.scene != activeScene)
                    continue;
                if (d.panelRoot == null)
                    continue;
                return d;
            }

            return null;
        }
    }

    public static bool IsShowing => Instance != null && Instance._isShowing;

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    Action _onConfirm;
    Action _onCancel;
    bool _isShowing;

    void Awake()
    {
        if (panelRoot == null)
            Debug.LogError($"ConfirmationDialog on '{name}': assign Panel Root.", this);
        if (messageText == null)
            Debug.LogError($"ConfirmationDialog on '{name}': assign Message Text.", this);
        if (confirmButton == null)
            Debug.LogError($"ConfirmationDialog on '{name}': assign Confirm Button.", this);
        if (cancelButton == null)
            Debug.LogError($"ConfirmationDialog on '{name}': assign Cancel Button.", this);

        confirmButton.onClick.AddListener(OnConfirmPressed);
        cancelButton.onClick.AddListener(OnCancelPressed);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void OnEnable() => RegisterSelf();

    void OnDisable()
    {
        UnregisterSelf();
        HideImmediate();
    }

    void OnDestroy()
    {
        UnregisterSelf();
        ClearCallbacks();
    }

    /// <summary>
    /// Shows the active-scene dialog, or <paramref name="dialog"/> when assigned.
    /// Returns false if no dialog is available (callbacks are not invoked).
    /// </summary>
    public static bool TryShow(
        string message,
        Action onConfirm,
        Action onCancel = null,
        ConfirmationDialog dialog = null)
    {
        var target = dialog != null ? dialog : Instance;
        if (target == null)
        {
            Debug.LogError(
                "ConfirmationDialog.TryShow: no ConfirmationDialog in the active scene. Add one to the canvas or pass a reference.");
            return false;
        }

        target.Show(message, onConfirm, onCancel);
        return true;
    }

    public void Show(string message, Action onConfirm, Action onCancel = null)
    {
        if (panelRoot == null || messageText == null || confirmButton == null || cancelButton == null)
        {
            Debug.LogError($"ConfirmationDialog on '{name}': missing required references — cannot show.", this);
            return;
        }

        ClearCallbacks();

        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _isShowing = true;

        messageText.text = message ?? string.Empty;

        panelRoot.transform.SetAsLastSibling();
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        HideImmediate();
        ClearCallbacks();
    }

    void OnConfirmPressed()
    {
        var confirm = _onConfirm;
        Hide();
        confirm?.Invoke();
    }

    void OnCancelPressed()
    {
        var cancel = _onCancel;
        Hide();
        cancel?.Invoke();
    }

    void HideImmediate()
    {
        _isShowing = false;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void ClearCallbacks()
    {
        _onConfirm = null;
        _onCancel = null;
    }

    static void RegisterSelf(ConfirmationDialog dialog)
    {
        CleanupDestroyedEntries();
        if (!Registry.Contains(dialog))
            Registry.Add(dialog);
    }

    void RegisterSelf() => RegisterSelf(this);

    static void UnregisterSelf(ConfirmationDialog dialog) => Registry.Remove(dialog);

    void UnregisterSelf() => UnregisterSelf(this);

    static void CleanupDestroyedEntries()
    {
        for (var i = Registry.Count - 1; i >= 0; i--)
        {
            if (Registry[i] == null)
                Registry.RemoveAt(i);
        }
    }
}
