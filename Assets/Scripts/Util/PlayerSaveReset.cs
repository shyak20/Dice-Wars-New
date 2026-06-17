using UnityEngine;

/// <summary>
/// Clears install-local player data. Use instead of calling <see cref="PlayerPrefs.DeleteAll"/> alone
/// so obfuscated progression PlayerPrefs and any legacy JSON files are wiped too.
/// </summary>
public static class PlayerSaveReset
{
    public static void DeleteAllPlayerPrefsAndProgression()
    {
        ProgressionManager.ClearAllSavedProgress();
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
