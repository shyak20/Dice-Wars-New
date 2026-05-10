using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Scene-local playlist: uses two <see cref="AudioSource"/>s and crossfades when advancing tracks.
/// Add this to a GameObject in each scene (or flow) that should drive its own music; it does not persist across loads.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PersistentMusicPlaylist : MonoBehaviour
{
    public const string MusicMutedPlayerPrefKey = MusicMuteStore.PlayerPrefKey;

    [SerializeField] private AudioClip[] tracks;
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [Tooltip("Crossfade duration when switching to the next playlist clip.")]
    [SerializeField, Min(0f)] private float crossfadeSeconds = 1f;
    [Tooltip("When true, mute toggles are written to PlayerPrefs via MusicMuteStore.")]
    [SerializeField] private bool persistMutePreference = true;

    private AudioSource _primary;
    private AudioSource _secondary;
    private AudioSource _audible;

    private Coroutine _sequence;
    private int _nextIndex;

    /// <summary>Fired after mute state changes (including when MusicMuteStore broadcasts).</summary>
    public event Action<bool> MuteStateChanged;

    public bool IsMuted => MusicMuteStore.IsMuted;

    private void Awake()
    {
        _primary = GetComponent<AudioSource>();
        if (_primary == null)
        {
            Debug.LogError("PersistentMusicPlaylist: AudioSource missing despite RequireComponent.", this);
            enabled = false;
            return;
        }

        ConfigureSource(_primary);
        _secondary = gameObject.AddComponent<AudioSource>();
        ConfigureSource(_secondary);

        MusicMuteStore.Changed += OnMuteStoreChanged;
        SyncSourcesMuteFromStore();

        if (FindObjectOfType<AudioListener>() == null)
            Debug.LogError("PersistentMusicPlaylist: no AudioListener in the scene — add one (e.g. on the main camera).", this);

        if (tracks == null || tracks.Length == 0)
            Debug.LogError("PersistentMusicPlaylist: assign at least one AudioClip in Tracks.", this);

        MuteStateChanged?.Invoke(MusicMuteStore.IsMuted);
    }

    private void OnDestroy()
    {
        MusicMuteStore.Changed -= OnMuteStoreChanged;
    }

    private static void ConfigureSource(AudioSource a)
    {
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f;
    }

    private void Start()
    {
        if (!playOnStart || tracks == null || tracks.Length == 0)
            return;
        BeginPlaylist();
    }

    private void OnMuteStoreChanged(bool muted)
    {
        SyncSourcesMuteFromStore();
        MuteStateChanged?.Invoke(muted);
    }

    private void SyncSourcesMuteFromStore()
    {
        if (_primary != null)
        {
            _primary.mute = MusicMuteStore.IsMuted;
            _primary.volume = volume;
        }
        if (_secondary != null)
        {
            _secondary.mute = MusicMuteStore.IsMuted;
            _secondary.volume = volume;
        }
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

        _primary.Stop();
        _secondary.Stop();
        _audible = null;
        SyncSourcesMuteFromStore();
    }

    public void SetVolume(float normalized)
    {
        volume = Mathf.Clamp01(normalized);
        SyncSourcesMuteFromStore();
    }

    public void SetMuted(bool muted)
    {
        MusicMuteStore.SetMuted(muted, persistMutePreference);
    }

    public void ToggleMuted() => MusicMuteStore.ToggleMuted(persistMutePreference);

    private IEnumerator PlaySequence()
    {
        while (true)
        {
            if (!TryAdvanceToNextNonNullTrack(out var clip))
                yield break;

            yield return CrossfadeTo(clip);

            var active = _audible;
            if (active == null || !active.isPlaying)
            {
                Debug.LogError($"PersistentMusicPlaylist: clip '{clip.name}' did not stay playing (check import settings / AudioListener).", this);
                continue;
            }

            yield return new WaitWhile(() => active.isPlaying);
        }
    }

    private IEnumerator CrossfadeTo(AudioClip clip)
    {
        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        var from = _audible;
        var to = from == _primary ? _secondary : _primary;

        to.clip = clip;
        to.time = 0f;
        to.volume = 0f;
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
            Debug.LogError($"PersistentMusicPlaylist: clip '{clip.name}' did not start playing (check import settings / AudioListener).", this);
            yield break;
        }

        var targetVol = MusicMuteStore.IsMuted ? 0f : volume;
        var dur = Mathf.Max(0f, crossfadeSeconds);

        if (from == null || !from.isPlaying)
        {
            if (dur <= 0f)
                to.volume = targetVol;
            else
            {
                var t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    var k = Mathf.Clamp01(t / dur);
                    to.volume = Mathf.Lerp(0f, targetVol, k);
                    yield return null;
                }
            }

            to.volume = targetVol;
            _audible = to;
            yield break;
        }

        var fromStart = from.volume;
        if (dur <= 0f)
        {
            from.Stop();
            from.volume = volume;
            to.volume = targetVol;
            _audible = to;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            var k = Mathf.Clamp01(elapsed / dur);
            from.volume = Mathf.Lerp(fromStart, 0f, k);
            to.volume = Mathf.Lerp(0f, targetVol, k);
            yield return null;
        }

        from.Stop();
        from.volume = volume;
        to.volume = targetVol;
        _audible = to;
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
