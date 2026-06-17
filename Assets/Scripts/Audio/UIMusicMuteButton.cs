using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI control that toggles <see cref="PersistentMusicPlaylist"/> mute. Safe on any scene: waits for the playlist bootstrap object.
/// Optional icon swaps for muted / unmuted art.
/// </summary>
public sealed class UIMusicMuteButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite iconWhenUnmuted;
    [SerializeField] private Sprite iconWhenMuted;

    private PersistentMusicPlaylist _subscribed;
    private Coroutine _bindRoutine;

    private void Awake()
    {
        if (button == null)
            throw new System.InvalidOperationException($"UIMusicMuteButton on '{name}' requires a Button reference.");

        button.onClick.AddListener(OnClicked);
    }

    private void OnEnable()
    {
        if (_bindRoutine != null)
            StopCoroutine(_bindRoutine);
        _bindRoutine = StartCoroutine(BindWhenPlaylistExists());
    }

    private void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        Unsubscribe();
    }

    private IEnumerator BindWhenPlaylistExists()
    {
        yield return new WaitUntil(() => PersistentMusicPlaylist.Instance != null);
        _bindRoutine = null;
        _subscribed = PersistentMusicPlaylist.Instance;
        _subscribed.MuteStateChanged += OnMuteStateChanged;
        ApplyVisual(_subscribed.IsMuted);
    }

    private void Unsubscribe()
    {
        if (_subscribed == null)
            return;
        _subscribed.MuteStateChanged -= OnMuteStateChanged;
        _subscribed = null;
    }

    private void OnClicked()
    {
        var p = PersistentMusicPlaylist.Instance;
        if (p == null)
            return;
        p.ToggleMuted();
        ApplyVisual(p.IsMuted);
    }

    private void OnMuteStateChanged(bool muted) => ApplyVisual(muted);

    private void ApplyVisual(bool muted)
    {
        if (icon == null)
            return;
        var s = muted ? iconWhenMuted : iconWhenUnmuted;
        if (s != null)
            icon.sprite = s;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }
}
