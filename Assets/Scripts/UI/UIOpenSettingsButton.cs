using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opens Settings UI from a button click.
/// Supports either:
/// 1) toggling an existing settings root in scene, or
/// 2) instantiating a settings prefab once and reusing it.
/// </summary>
public sealed class UIOpenSettingsButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Existing settings root in scene (preferred when already present).")]
    [SerializeField] private GameObject settingsRoot;
    [Tooltip("Optional prefab to spawn if settingsRoot is not assigned.")]
    [SerializeField] private GameObject settingsPrefab;
    [Tooltip("Parent for instantiated settings prefab; defaults to this button's parent canvas.")]
    [SerializeField] private Transform settingsParent;
    [Tooltip("When true, clicking while settings are open will close them.")]
    [SerializeField] private bool toggleIfAlreadyOpen = true;

    private GameObject _runtimeSettingsInstance;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError($"UIOpenSettingsButton on '{name}': assign a Button or place this on a Button object.", this);
            return;
        }

        if (settingsRoot == null && settingsPrefab == null)
        {
            Debug.LogError($"UIOpenSettingsButton on '{name}': assign settingsRoot or settingsPrefab.", this);
            return;
        }

        button.onClick.AddListener(OnPressed);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnPressed);
    }

    private void OnPressed()
    {
        var target = ResolveSettingsObject();
        if (target == null)
            return;

        if (toggleIfAlreadyOpen && target.activeSelf)
            target.SetActive(false);
        else
            target.SetActive(true);
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
        _runtimeSettingsInstance.SetActive(true);
        return _runtimeSettingsInstance;
    }
}
