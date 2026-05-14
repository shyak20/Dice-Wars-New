using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// When enabled, asks <see cref="PersistentMusicPlaylist"/> to load <see cref="sceneMusicClip"/> only on the
/// clear music bus and crossfade from the current line. When <see cref="onlyWhenThisSceneIsActive"/> is true,
/// waits until this GameObject's scene is the active scene (next frame and/or <see cref="SceneManager.activeSceneChanged"/>)
/// so additive and async loads still get a crossfade.
/// </summary>
public sealed class SceneMusicOnEnable : MonoBehaviour
{
    [SerializeField] private AudioClip sceneMusicClip;
    [SerializeField, Min(0.01f)] private float crossfadeDurationSeconds = 1.5f;
    [SerializeField] private bool loop = true;
    [SerializeField, Min(0f)]
    [Tooltip("Wait this long for PersistentMusicPlaylist (Main Menu bootstrap) before logging an error.")]
    private float waitForPlaylistSeconds = 5f;

    [SerializeField]
    [Tooltip("If true, crossfade runs only when this object's scene is SceneManager.GetActiveScene(). If not yet active (e.g. additive load), waits for activeSceneChanged.")]
    private bool onlyWhenThisSceneIsActive = true;

    private Coroutine _waitRoutine;
    private bool _listeningForActiveScene;

    private void OnEnable()
    {
        if (sceneMusicClip == null)
        {
            Debug.LogError($"SceneMusicOnEnable on '{name}': assign Scene Music Clip.", this);
            return;
        }

        if (_waitRoutine != null)
            StopCoroutine(_waitRoutine);
        _waitRoutine = StartCoroutine(CoCrossfadeWhenReady());
    }

    private void OnDisable()
    {
        if (_waitRoutine != null)
        {
            StopCoroutine(_waitRoutine);
            _waitRoutine = null;
        }

        UnregisterActiveSceneListener();
    }

    private void UnregisterActiveSceneListener()
    {
        if (!_listeningForActiveScene)
            return;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        _listeningForActiveScene = false;
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (!isActiveAndEnabled || !onlyWhenThisSceneIsActive)
            return;
        if (gameObject.scene != next)
            return;

        UnregisterActiveSceneListener();
        TryCrossfade();
    }

    private void TryCrossfade()
    {
        if (sceneMusicClip == null)
            return;
        if (PersistentMusicPlaylist.Instance == null)
            return;
        PersistentMusicPlaylist.Instance.CrossfadeToClip(sceneMusicClip, crossfadeDurationSeconds, loop);
    }

    private IEnumerator CoCrossfadeWhenReady()
    {
        var deadline = Time.unscaledTime + waitForPlaylistSeconds;
        while (PersistentMusicPlaylist.Instance == null && Time.unscaledTime < deadline)
            yield return null;

        _waitRoutine = null;

        if (PersistentMusicPlaylist.Instance == null)
        {
            Debug.LogError(
                $"SceneMusicOnEnable on '{name}': PersistentMusicPlaylist did not appear within {waitForPlaylistSeconds}s. " +
                "Add a bootstrap with PersistentMusicPlaylist in an earlier scene (e.g. main menu).",
                this);
            yield break;
        }

        yield return null;

        if (!onlyWhenThisSceneIsActive)
        {
            TryCrossfade();
            yield break;
        }

        if (gameObject.scene == SceneManager.GetActiveScene())
        {
            TryCrossfade();
            yield break;
        }

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        _listeningForActiveScene = true;
    }
}
