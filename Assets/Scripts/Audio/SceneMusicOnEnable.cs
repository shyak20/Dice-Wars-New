using System.Collections;
using UnityEngine;

/// <summary>
/// Place one instance per scene (e.g. on a bootstrap object). When enabled, crossfades
/// <see cref="PersistentMusicPlaylist"/> from whatever is playing into <see cref="sceneMusicClip"/>.
/// </summary>
public sealed class SceneMusicOnEnable : MonoBehaviour
{
    [SerializeField] private AudioClip sceneMusicClip;
    [SerializeField, Min(0.01f)] private float crossfadeDurationSeconds = 1.5f;
    [SerializeField] private bool loop = true;
    [Tooltip("If the playlist is already playing this exact clip, skip starting another crossfade.")]
    [SerializeField] private bool skipIfAlreadyPlayingThisClip = true;

    Coroutine _waitRoutine;

    private void OnEnable()
    {
        if (sceneMusicClip == null)
        {
            Debug.LogError($"SceneMusicOnEnable on '{name}': assign Scene Music Clip.", this);
            return;
        }

        if (_waitRoutine != null)
            StopCoroutine(_waitRoutine);
        _waitRoutine = StartCoroutine(CoCrossfadeWhenPlaylistReady());
    }

    private void OnDisable()
    {
        if (_waitRoutine != null)
        {
            StopCoroutine(_waitRoutine);
            _waitRoutine = null;
        }
    }

    IEnumerator CoCrossfadeWhenPlaylistReady()
    {
        yield return new WaitUntil(() => PersistentMusicPlaylist.Instance != null);
        _waitRoutine = null;

        var p = PersistentMusicPlaylist.Instance;
        if (skipIfAlreadyPlayingThisClip && p.IsPlayingClip(sceneMusicClip))
            yield break;

        p.CrossfadeToClip(sceneMusicClip, crossfadeDurationSeconds, loop);
    }
}
