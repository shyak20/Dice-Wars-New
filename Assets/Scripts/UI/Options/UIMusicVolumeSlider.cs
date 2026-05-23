using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Binds a UI slider to <see cref="PersistentMusicPlaylist"/> master volume (persisted in PlayerPrefs).</summary>
public sealed class UIMusicVolumeSlider : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text valueLabel;
    [Tooltip("When set, formats label as percentage (e.g. 75%).")]
    [SerializeField] private bool showValueAsPercent = true;

    bool _ignoreSliderCallback;
    Coroutine _bindRoutine;

    void Awake()
    {
        if (volumeSlider == null)
            volumeSlider = GetComponent<Slider>();

        if (volumeSlider == null)
            Debug.LogError($"UIMusicVolumeSlider on '{name}': assign volumeSlider.", this);
        else
            volumeSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    void OnEnable()
    {
        if (_bindRoutine != null)
            StopCoroutine(_bindRoutine);
        _bindRoutine = StartCoroutine(BindWhenPlaylistExists());
    }

    void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }
    }

    void OnDestroy()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    public void RefreshFromPlaylist()
    {
        var playlist = PersistentMusicPlaylist.Instance;
        if (playlist == null || volumeSlider == null)
            return;

        _ignoreSliderCallback = true;
        volumeSlider.SetValueWithoutNotify(playlist.MusicVolume);
        _ignoreSliderCallback = false;
        UpdateLabel(playlist.MusicVolume);
    }

    IEnumerator BindWhenPlaylistExists()
    {
        yield return new WaitUntil(() => PersistentMusicPlaylist.Instance != null);
        _bindRoutine = null;
        RefreshFromPlaylist();
    }

    void OnSliderChanged(float value)
    {
        if (_ignoreSliderCallback)
            return;

        var playlist = PersistentMusicPlaylist.Instance;
        if (playlist == null)
            return;

        playlist.SetVolume(value);
        UpdateLabel(value);
    }

    void UpdateLabel(float normalized)
    {
        if (valueLabel == null)
            return;

        valueLabel.text = showValueAsPercent
            ? $"{Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f)}%"
            : normalized.ToString("0.##");
    }
}
