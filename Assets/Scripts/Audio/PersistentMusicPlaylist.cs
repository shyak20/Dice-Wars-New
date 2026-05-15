using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DontDestroyOnLoad music host with two fixed <see cref="AudioSource"/> buses: <b>Audio A</b> (on this GameObject)
/// and <b>Audio B</b> (added in Awake). The playlist starts on A only; B stays empty until the first crossfade.
/// Each new clip (next playlist track or <see cref="CrossfadeToClip"/>) loads only on the clear bus (the one not
/// carrying the current line), then crossfades from the outgoing bus over the requested duration.
/// Scene load flows call <see cref="TryBeginCrossfadeForSceneNamed"/> before <c>SceneManager.LoadScene</c>; scenes may host <see cref="SceneMusicTarget"/>.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PersistentMusicPlaylist : MonoBehaviour
{
    public const string MusicMutedPlayerPrefKey = "DiceWars_MusicMuted";

    public static PersistentMusicPlaylist Instance { get; private set; }

    [SerializeField] private AudioClip[] tracks;
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Min(0.01f)] private float trackToTrackCrossfadeSeconds = 1.5f;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [Tooltip("When true, PlayerPrefs remembers mute across runs.")]
    [SerializeField] private bool persistMutePreference = true;

    [Header("Scene transitions")]
    [Tooltip("Optional. Scene names (Scene.name) → clip/crossfade for transitions before that scene has loaded once.")]
    [SerializeField] private SceneMusicCatalogSO sceneMusicCatalog;

    private readonly Dictionary<string, SceneMusicRegistration> _sceneMusicBySceneName =
        new Dictionary<string, SceneMusicRegistration>(StringComparer.OrdinalIgnoreCase);

    private struct SceneMusicRegistration
    {
        public AudioClip Clip;
        public float CrossfadeSeconds;
        public bool Loop;
    }

    /// <summary>Audio A — the component on this GameObject. First playlist track starts here.</summary>
    private AudioSource _audioA;

    /// <summary>Audio B — second bus, always present; stays clear until the first crossfade.</summary>
    private AudioSource _audioB;

    /// <summary>When true, the audible line is on A; when false, on B.</summary>
    private bool _lineOnA = true;

    private AudioSource _crossfadeFrom;
    private AudioSource _crossfadeTo;

    private int _nextIndex;
    private Coroutine _sequence;
    private Coroutine _crossfadeRoutine;
    private bool _muted;

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

        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);

        _audioA = GetComponent<AudioSource>();
        if (_audioA == null)
        {
            Debug.LogError("PersistentMusicPlaylist: AudioSource missing despite RequireComponent.", this);
            enabled = false;
            return;
        }

        _audioB = gameObject.AddComponent<AudioSource>();

        foreach (var s in new[] { _audioA, _audioB })
        {
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
        }

        if (persistMutePreference && PlayerPrefs.GetInt(MusicMutedPlayerPrefKey, 0) != 0)
            _muted = true;

        ApplyMuteAndVolume();
        MuteStateChanged?.Invoke(_muted);

        if (FindObjectOfType<AudioListener>() == null)
            Debug.LogError("PersistentMusicPlaylist: no AudioListener in the scene — add one (e.g. on the main camera).", this);

        if (tracks == null || tracks.Length == 0)
            Debug.LogError("PersistentMusicPlaylist: assign at least one AudioClip in Tracks.", this);

        MergeSceneMusicCatalogIntoRegistry();
    }

    /// <summary>Merges <see cref="sceneMusicCatalog"/> entries (does not clear overrides from <see cref="SceneMusicTarget"/>).</summary>
    public void MergeSceneMusicCatalogIntoRegistry()
    {
        if (sceneMusicCatalog == null || sceneMusicCatalog.entries == null)
            return;

        for (var i = 0; i < sceneMusicCatalog.entries.Count; i++)
        {
            var e = sceneMusicCatalog.entries[i];
            if (e == null || string.IsNullOrWhiteSpace(e.sceneName) || e.clip == null)
                continue;

            var key = NormalizeSceneKey(e.sceneName);
            if (string.IsNullOrEmpty(key))
                continue;

            _sceneMusicBySceneName[key] = new SceneMusicRegistration
            {
                Clip = e.clip,
                CrossfadeSeconds = Mathf.Max(0.01f, e.crossfadeDurationSeconds),
                Loop = e.loop,
            };
        }
    }

    /// <summary>Per-scene bootstrap from <see cref="SceneMusicTarget"/> (overrides catalog for that scene name).</summary>
    public void RegisterSceneMusic(string sceneName, AudioClip clip, float crossfadeDurationSeconds, bool loop)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || clip == null)
            return;

        var key = NormalizeSceneKey(sceneName);
        if (string.IsNullOrEmpty(key))
            return;

        _sceneMusicBySceneName[key] = new SceneMusicRegistration
        {
            Clip = clip,
            CrossfadeSeconds = Mathf.Max(0.01f, crossfadeDurationSeconds),
            Loop = loop,
        };
    }

    /// <summary>
    /// Starts crossfade toward the clip registered for <paramref name="sceneName"/> (from catalog or <see cref="SceneMusicTarget"/>).
    /// Call immediately before <c>SceneManager.LoadScene</c> for single-loaded gameplay scenes.
    /// </summary>
    public void TryBeginCrossfadeForSceneNamed(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        var key = NormalizeSceneKey(sceneName);
        if (string.IsNullOrEmpty(key))
            return;

        if (!_sceneMusicBySceneName.TryGetValue(key, out var reg) || reg.Clip == null)
            return;

        CrossfadeToClip(reg.Clip, reg.CrossfadeSeconds, reg.Loop);
    }

    static string NormalizeSceneKey(string sceneName) =>
        string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();

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

    /// <summary>Outgoing bus (current line). Incoming clip always targets the other bus.</summary>
    private AudioSource OutgoingBus => _lineOnA ? _audioA : _audioB;

    /// <summary>Clear bus — receives the next clip only; no duplicate load on the outgoing bus.</summary>
    private AudioSource IncomingBus => _lineOnA ? _audioB : _audioA;

    private AudioSource LineBus => _lineOnA ? _audioA : _audioB;

    public void BeginPlaylist()
    {
        if (tracks == null || tracks.Length == 0)
            return;

        StopCrossfadeRoutineIfAny();
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }

        SnapMidCrossfadeIfNeeded();

        _audioA.Stop();
        _audioB.Stop();
        _audioB.clip = null;
        _audioA.clip = null;
        _lineOnA = true;

        ApplyMuteAndVolume();

        _sequence = StartCoroutine(PlaySequence());
    }

    public void StopPlaylistAdvancementOnly()
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }
    }

    public void StopPlaylist()
    {
        StopPlaylistAdvancementOnly();
        StopCrossfadeRoutineIfAny();
        SnapMidCrossfadeIfNeeded();

        _audioA.Stop();
        _audioB.Stop();
        _audioA.clip = null;
        _audioB.clip = null;
        ApplyMuteAndVolume();
    }

    public void CrossfadeToClip(AudioClip clip, float crossfadeDurationSeconds, bool loop = true)
    {
        if (clip == null)
        {
            Debug.LogError("PersistentMusicPlaylist.CrossfadeToClip: clip is null.", this);
            return;
        }

        StopCrossfadeRoutineIfAny();
        StopPlaylistAdvancementOnly();
        SnapMidCrossfadeIfNeeded();
        _crossfadeRoutine = StartCoroutine(CoCrossfadeToClip(clip, crossfadeDurationSeconds, loop));
    }

    public bool IsPlayingClip(AudioClip clip)
    {
        if (clip == null)
            return false;
        return BusPlayingClip(_audioA, clip) || BusPlayingClip(_audioB, clip);
    }

    private static bool BusPlayingClip(AudioSource s, AudioClip clip) =>
        s != null && s.isPlaying && s.clip == clip;

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
            if (s == _crossfadeFrom || s == _crossfadeTo)
                return;
            s.volume = volume;
        }

        ApplyOne(_audioA);
        ApplyOne(_audioB);
    }

    private void StopCrossfadeRoutineIfAny()
    {
        if (_crossfadeRoutine == null)
            return;
        StopCoroutine(_crossfadeRoutine);
        _crossfadeRoutine = null;
        SnapMidCrossfadeIfNeeded();
    }

    /// <summary>If a crossfade coroutine was stopped mid-fade, snap to the intended incoming bus and clear the outgoing line.</summary>
    private void SnapMidCrossfadeIfNeeded()
    {
        if (_crossfadeTo == null)
            return;

        var from = _crossfadeFrom;
        var to = _crossfadeTo;
        _crossfadeFrom = null;
        _crossfadeTo = null;

        if (from != null)
        {
            from.Stop();
            from.clip = null;
        }

        if (to.clip != null && !to.isPlaying)
            to.Play();

        to.volume = _muted ? 0f : volume;
        to.mute = _muted;
        _lineOnA = to == _audioA;
        ApplyMuteAndVolume();
    }

    private IEnumerator CoCrossfadeToClip(AudioClip clip, float crossfadeDurationSeconds, bool loop)
    {
        yield return CoCrossfadeBetweenBuses(clip, crossfadeDurationSeconds, loop);
        _crossfadeRoutine = null;
    }

    private IEnumerator CoCrossfadeBetweenBuses(AudioClip clip, float crossfadeDurationSeconds, bool loop)
    {
        SnapMidCrossfadeIfNeeded();

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        var from = OutgoingBus;
        var to = IncomingBus;

        _crossfadeFrom = from;
        _crossfadeTo = to;

        to.Stop();
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
            Debug.LogError($"PersistentMusicPlaylist: incoming clip '{clip.name}' did not start playing.", this);
            to.Stop();
            to.clip = null;
            _crossfadeFrom = null;
            _crossfadeTo = null;
            ApplyMuteAndVolume();
            yield break;
        }

        var fromVolStart = !_muted && from.isPlaying && from.clip != null ? from.volume : 0f;

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
        from.clip = null;

        _lineOnA = to == _audioA;
        _crossfadeFrom = null;
        _crossfadeTo = null;

        ApplyMuteAndVolume();
    }

    private IEnumerator PlaySequence()
    {
        if (!TryAdvanceToNextNonNullTrack(out var first))
            yield break;

        _audioB.Stop();
        _audioB.clip = null;
        _lineOnA = true;

        _audioA.clip = first;
        _audioA.loop = false;
        _audioA.time = 0f;
        ApplyMuteAndVolume();
        _audioA.Play();

        if (!_audioA.isPlaying)
        {
            Debug.LogError($"PersistentMusicPlaylist: first clip '{first?.name}' did not start playing.", this);
            yield break;
        }

        yield return new WaitWhile(() => _audioA.isPlaying);

        while (true)
        {
            if (!TryAdvanceToNextNonNullTrack(out var clip))
                yield break;

            yield return CoCrossfadeBetweenBuses(clip, trackToTrackCrossfadeSeconds, loop: false);

            var line = LineBus;
            if (!line.isPlaying || line.clip != clip)
            {
                Debug.LogError($"PersistentMusicPlaylist: clip '{clip?.name}' did not land on the line bus.", this);
                continue;
            }

            yield return new WaitWhile(() => line.isPlaying);
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
