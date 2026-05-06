using UnityEngine;

/// <summary>
/// Scene-level ESC shortcut for settings popup:
/// - ESC opens settings when closed
/// - ESC closes settings when open
/// Supports either an existing scene root or a prefab instantiated once at runtime.
/// </summary>
public sealed class UISettingsEscapeToggle : MonoBehaviour
{
    [Tooltip("Existing settings root in scene (preferred).")]
    [SerializeField] private GameObject settingsRoot;
    [Tooltip("Optional prefab to instantiate when settingsRoot is not assigned.")]
    [SerializeField] private GameObject settingsPrefab;
    [Tooltip("Parent for instantiated settings prefab; defaults to this object's parent Canvas.")]
    [SerializeField] private Transform settingsParent;

    private GameObject _runtimeSettingsInstance;

    private void Awake()
    {
        if (settingsRoot == null && settingsPrefab == null)
        {
            Debug.LogError($"UISettingsEscapeToggle on '{name}': assign settingsRoot or settingsPrefab.", this);
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        var target = ResolveSettingsObject();
        if (target == null)
            return;

        target.SetActive(!target.activeSelf);
    }

    private GameObject ResolveSettingsObject()
    {
        if (settingsRoot != null)
            return settingsRoot;

        if (_runtimeSettingsInstance != null)
            return _runtimeSettingsInstance;

        if (settingsPrefab == null)
            return null;

        var parent = settingsParent;
        if (parent == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            parent = canvas != null ? canvas.transform : transform.parent;
        }

        _runtimeSettingsInstance = Instantiate(settingsPrefab, parent);
        _runtimeSettingsInstance.name = settingsPrefab.name;
        _runtimeSettingsInstance.SetActive(false);
        return _runtimeSettingsInstance;
    }
}
