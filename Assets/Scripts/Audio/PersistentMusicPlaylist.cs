using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Plays an ordered list of music clips on the primary <see cref="AudioSource"/> across scene loads.
/// A second source is used for <see cref="CrossfadeToClip"/> (per-scene <see cref="SceneMusicOnEnable"/>).
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
    private AudioSource _secondary;
    private int _nextIndex;
    private Coroutine _sequence;
    private Coroutine _crossfadeRoutine;
    private bool _muted;
    /// <summary>When true, the primary source is the one that should carry the active music line (playlist or last crossfade landing).</summary>
    private bool _playingIsPrimary = true;

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

        _secondary = gameObject.AddComponent<AudioSource>();
        _secondary.playOnAwake = false;
        _secondary.loop = false;
        _secondary.spatialBlend = 0f;

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

    /// <summary>Begins or restarts the playlist from the current next index (0 on first run). Stops scene crossfade music on the secondary bus.</summary>
    public void BeginPlaylist()
    {
        if (tracks == null || tracks.Length == 0)
            return;

        if (_crossfadeRoutine != null)
        {
            StopCoroutine(_crossfadeRoutine);
            _crossfadeRoutine = null;
        }

        if (_secondary != null)
            _secondary.Stop();

        _playingIsPrimary = true;

        if (_audio != null)
            _audio.Stop();

        if (_sequence != null)
            StopCoroutine(_sequence);
        _sequence = StartCoroutine(PlaySequence());
    }

    /// <summary>Stops playlist advancement and stops playback on the primary source only.</summary>
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

    /// <summary>Fades out whichever source is currently audible and fades in <paramref name="clip"/> on the other bus. Stops the ordered playlist.</summary>
    public void CrossfadeToClip(AudioClip clip, float crossfadeDurationSeconds, bool loop = true)
    {
        if (clip == null)
        {
            Debug.LogError("PersistentMusicPlaylist.CrossfadeToClip: clip is null.", this);
            return;
        }

        if (_crossfadeRoutine != null)
        {
            StopCoroutine(_crossfadeRoutine);
            _crossfadeRoutine = null;
        }

        _crossfadeRoutine = StartCoroutine(CoCrossfadeToClip(clip, crossfadeDurationSeconds, loop));
    }

    /// <summary>True if either music bus is playing this clip (after crossfade settles).</summary>
    public bool IsPlayingClip(AudioClip clip)
    {
        if (clip == null)
            return false;
        if (_audio != null && _audio.isPlaying && _audio.clip == clip)
            return true;
        return _secondary != null && _secondary.isPlaying && _secondary.clip == clip;
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
        void ApplyOne(AudioSource s)
        {
            if (s == null)
                return;
            s.mute = _muted;
            s.volume = volume;
        }

        ApplyOne(_audio);
        ApplyOne(_secondary);
    }

    private IEnumerator CoCrossfadeToClip(AudioClip clip, float crossfadeDurationSeconds, bool loop)
    {
        StopPlaylist();

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        var from = _playingIsPrimary ? _audio : _secondary;
        var to = _playingIsPrimary ? _secondary : _audio;

        var fromVolStart = !_muted && from != null && from.isPlaying ? from.volume : 0f;

        to.clip = clip;
        to.loop = loop;
        to.time = 0f;
        to.volume = 0f;
        to.mute = _muted;
        to.Play();

        var waited = 0f;
        const float giveUp = 2f;
        while (!to.isPlaying && waited < giveUp)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!to.isPlaying)
        {
            Debug.LogError($"PersistentMusicPlaylist: crossfade target clip '{clip.name}' did not start playing.", this);
            _crossfadeRoutine = null;
            yield break;
        }

        var dur = Mathf.Max(0.01f, crossfadeDurationSeconds);
        var elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(elapsed / dur);
            var smooth = u * u * (3f - 2f * u);

            if (_muted)
            {
                from.volume = 0f;
                to.volume = 0f;
            }
            else
            {
                from.volume = Mathf.Lerp(fromVolStart, 0f, smooth);
                to.volume = Mathf.Lerp(0f, volume, smooth);
            }

            yield return null;
        }

        from.Stop();
        ApplyMuteAndVolume();
        _playingIsPrimary = to == _audio;
        _crossfadeRoutine = null;
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
