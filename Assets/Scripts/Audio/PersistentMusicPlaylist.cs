using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a ordered list of music clips on one <see cref="AudioSource"/> and keeps playing across scene loads.
/// When a clip finishes, the next clip plays; after the last clip, playback continues from the first.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PersistentMusicPlaylist : MonoBehaviour
{
    public const string MusicMutedPlayerPrefKey = "DiceWars_MusicMuted";

    public static PersistentMusicPlaylist Instance { get; private set; }

    [SerializeField] private AudioClip[] tracks;
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [Tooltip("When true, PlayerPrefs remembers mute across runs.")]
    [SerializeField] private bool persistMutePreference = true;

    private AudioSource _audio;
    private int _nextIndex;
    private Coroutine _sequence;
    private bool _muted;

    /// <summary>Fired after mute state changes (including on startup when loaded from PlayerPrefs).</summary>
    public event Action<bool> MuteStateChanged;

    public bool IsMuted => _muted;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad only applies reliably to root objects; detach from parent first.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);

        _audio = GetComponent<AudioSource>();
        if (_audio == null)
        {
            Debug.LogError("PersistentMusicPlaylist: AudioSource missing despite RequireComponent.", this);
            enabled = false;
            return;
        }

        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 0f;

        if (persistMutePreference && PlayerPrefs.GetInt(MusicMutedPlayerPrefKey, 0) != 0)
            _muted = true;

        ApplyMuteAndVolume();
        MuteStateChanged?.Invoke(_muted);

        if (FindObjectOfType<AudioListener>() == null)
            Debug.LogError("PersistentMusicPlaylist: no AudioListener in the scene — add one (e.g. on the main camera).", this);

        if (tracks == null || tracks.Length == 0)
            Debug.LogError("PersistentMusicPlaylist: assign at least one AudioClip in Tracks.", this);
    }

    private void Start()
    {
        if (!playOnStart || tracks == null || tracks.Length == 0)
            return;
        BeginPlaylist();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Begins or restarts the playlist from the current next index (0 on first run).</summary>
    public void BeginPlaylist()
    {
        if (tracks == null || tracks.Length == 0)
            return;

        if (_sequence != null)
            StopCoroutine(_sequence);
        _sequence = StartCoroutine(PlaySequence());
    }

    /// <summary>Stops playback and cancels advancing to the next track.</summary>
    public void StopPlaylist()
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }

        if (_audio != null)
            _audio.Stop();
    }

    public void SetVolume(float normalized)
    {
        volume = Mathf.Clamp01(normalized);
        ApplyMuteAndVolume();
    }

    public void SetMuted(bool muted)
    {
        if (_muted == muted)
            return;
        _muted = muted;
        if (persistMutePreference)
            PlayerPrefs.SetInt(MusicMutedPlayerPrefKey, _muted ? 1 : 0);
        ApplyMuteAndVolume();
        MuteStateChanged?.Invoke(_muted);
    }

    public void ToggleMuted() => SetMuted(!_muted);

    private void ApplyMuteAndVolume()
    {
        if (_audio == null)
            return;
        _audio.volume = volume;
        _audio.mute = _muted;
    }

    private IEnumerator PlaySequence()
    {
        while (true)
        {
            if (!TryAdvanceToNextNonNullTrack(out var clip))
                yield break;

            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            _audio.clip = clip;
            _audio.Play();

            // Unity often reports isPlaying == false for the first frame after Play(); waiting only on isPlaying skips the whole clip.
            var waited = 0f;
            const float giveUp = 2f;
            while (!_audio.isPlaying && waited < giveUp)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_audio.isPlaying)
            {
                Debug.LogError($"PersistentMusicPlaylist: clip '{clip.name}' did not start playing (check import settings / AudioListener).", this);
                continue;
            }

            yield return new WaitWhile(() => _audio.isPlaying);
        }
    }

    private bool TryAdvanceToNextNonNullTrack(out AudioClip clip)
    {
        clip = null;
        if (tracks == null || tracks.Length == 0)
            return false;

        for (var attempt = 0; attempt < tracks.Length; attempt++)
        {
            var idx = _nextIndex;
            var candidate = tracks[_nextIndex];
            _nextIndex = (_nextIndex + 1) % tracks.Length;
            if (candidate != null)
            {
                clip = candidate;
                return true;
            }

            Debug.LogError($"PersistentMusicPlaylist: track at index {idx} is null. Skipping.", this);
        }

        Debug.LogError("PersistentMusicPlaylist: all track entries are null. Stopping.", this);
        return false;
    }
}
