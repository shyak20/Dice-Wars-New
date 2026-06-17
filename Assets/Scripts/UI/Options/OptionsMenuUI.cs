using UnityEngine;

/// <summary>
/// Settings / options panel shown via <see cref="UISettingsEscapeToggle"/> or <see cref="UIOpenSettingsButton"/>.
/// Refreshes run-only controls (e.g. abandon run) when opened.
/// </summary>
public sealed class OptionsMenuUI : MonoBehaviour
{
    [SerializeField] private UIMusicVolumeSlider musicVolumeSlider;
    [SerializeField] private UIAbandonRunButton abandonRunButton;
    [Tooltip("Panel root to hide when abandon run is confirmed (usually this settings screen).")]
    [SerializeField] private GameObject settingsRoot;

    void Awake()
    {
        if (settingsRoot == null)
            settingsRoot = gameObject;

        if (musicVolumeSlider == null)
            musicVolumeSlider = GetComponentInChildren<UIMusicVolumeSlider>(true);
        if (abandonRunButton == null)
            abandonRunButton = GetComponentInChildren<UIAbandonRunButton>(true);

        if (musicVolumeSlider == null)
            Debug.LogError($"OptionsMenuUI on '{name}': assign musicVolumeSlider or add UIMusicVolumeSlider under this panel.", this);
        if (abandonRunButton == null)
            Debug.LogError($"OptionsMenuUI on '{name}': assign abandonRunButton or add UIAbandonRunButton under this panel.", this);
    }

    void OnEnable() => Refresh();

    public void Refresh()
    {
        musicVolumeSlider?.RefreshFromPlaylist();
        abandonRunButton?.RefreshAvailability();
    }

    public void CloseSettings()
    {
        if (settingsRoot != null)
            settingsRoot.SetActive(false);
    }
}
