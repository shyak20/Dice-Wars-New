using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Place one active instance per scene that owns music (Map, Dice Select, Main Menu). Defines which clip and crossfade apply to this scene.
/// Registers with <see cref="PersistentMusicPlaylist"/> when the scene becomes relevant — skipped when this scene is loaded additively while another scene is active (Fight/Shop preload under the map).
/// Transitions should call <see cref="PersistentMusicPlaylist.TryBeginCrossfadeForSceneNamed"/> before <see cref="SceneManager.LoadScene(string)"/> so the crossfade starts early.
/// </summary>
public sealed class SceneMusicTarget : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;

    [SerializeField, Min(0.01f)] private float crossfadeDurationSeconds = 1.5f;

    [SerializeField] private bool loop = true;

    private void Awake()
    {
        TryRegisterThisSceneMusic();
    }

    /// <summary>Editor / runtime: push current inspector values for this scene into the playlist dictionary.</summary>
    public void TryRegisterThisSceneMusic()
    {
        var myScene = gameObject.scene;
        if (!myScene.IsValid())
            return;

        var active = SceneManager.GetActiveScene();
        if (myScene.isLoaded && active.IsValid() && myScene.handle != active.handle)
            return;

        if (musicClip == null)
        {
            Debug.LogWarning($"SceneMusicTarget on '{name}' in scene '{myScene.name}': assign Music Clip (or remove component).", this);
            return;
        }

        PersistentMusicPlaylist.Instance?.RegisterSceneMusic(
            myScene.name,
            musicClip,
            crossfadeDurationSeconds,
            loop);
    }
}
