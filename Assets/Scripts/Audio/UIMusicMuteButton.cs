using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI control that toggles <see cref="MusicMuteStore"/> mute. Works in any scene without a global music singleton.
/// Optional icon swaps for muted / unmuted art.
/// </summary>
public sealed class UIMusicMuteButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite iconWhenUnmuted;
    [SerializeField] private Sprite iconWhenMuted;

    private void Awake()
    {
        if (button == null)
            throw new System.InvalidOperationException($"UIMusicMuteButton on '{name}' requires a Button reference.");

        button.onClick.AddListener(OnClicked);
    }

    private void OnEnable()
    {
        MusicMuteStore.Changed += OnMuteStateChanged;
        ApplyVisual(MusicMuteStore.IsMuted);
    }

    private void OnDisable()
    {
        MusicMuteStore.Changed -= OnMuteStateChanged;
    }

    private void OnClicked()
    {
        MusicMuteStore.ToggleMuted();
        ApplyVisual(MusicMuteStore.IsMuted);
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
