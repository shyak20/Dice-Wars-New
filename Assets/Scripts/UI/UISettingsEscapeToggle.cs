using UnityEngine;

/// <summary>
/// Scene-level ESC shortcut: toggles <see cref="settingsRoot"/> open and closed.
/// </summary>
public sealed class UISettingsEscapeToggle : MonoBehaviour
{
    [SerializeField] private GameObject settingsRoot;

    private void Awake()
    {
        if (settingsRoot == null)
            Debug.LogError($"UISettingsEscapeToggle on '{name}': assign settingsRoot.", this);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || settingsRoot == null)
            return;

        settingsRoot.SetActive(!settingsRoot.activeSelf);
    }
}
