using System;
using UnityEngine;

/// <summary>
/// Run-wide music mute preference (PlayerPrefs). Scene-local playlist players subscribe for live updates.
/// </summary>
public static class MusicMuteStore
{
    public const string PlayerPrefKey = "DiceWars_MusicMuted";

    private static bool _initialized;
    private static bool _muted;

    public static bool IsMuted
    {
        get
        {
            EnsureInit();
            return _muted;
        }
    }

    public static event Action<bool> Changed;

    private static void EnsureInit()
    {
        if (_initialized)
            return;
        _muted = PlayerPrefs.GetInt(PlayerPrefKey, 0) != 0;
        _initialized = true;
    }

    public static void SetMuted(bool muted, bool persist = true)
    {
        EnsureInit();
        if (_muted == muted)
            return;
        _muted = muted;
        if (persist)
            PlayerPrefs.SetInt(PlayerPrefKey, _muted ? 1 : 0);
        Changed?.Invoke(_muted);
    }

    public static void ToggleMuted(bool persist = true) => SetMuted(!IsMuted, persist);
}
