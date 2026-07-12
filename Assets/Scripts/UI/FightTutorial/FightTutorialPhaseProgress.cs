using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Once-ever PlayerPrefs for fight-tutorial phases. Cleared with progression reset
/// via <see cref="ResetAllStoredProgress"/>.
/// </summary>
public static class FightTutorialPhaseProgress
{
    public const string PlayerPrefsKeyPrefix = "DiceWars_FightTutorialPhase_";
    const string RegistryPrefsKey = PlayerPrefsKeyPrefix + "_Registry";

    public static bool IsSeen(string phaseId)
    {
        if (string.IsNullOrWhiteSpace(phaseId))
            return false;
        return PlayerPrefs.GetInt(BuildStorageKey(phaseId), 0) != 0;
    }

    public static void MarkSeen(string phaseId)
    {
        if (string.IsNullOrWhiteSpace(phaseId))
            return;

        var key = BuildStorageKey(phaseId);
        if (PlayerPrefs.GetInt(key, 0) != 0)
            return;

        PlayerPrefs.SetInt(key, 1);
        RegisterStorageKey(key);
        PlayerPrefs.Save();
    }

    public static void ResetAllStoredProgress()
    {
        var keysToDelete = new HashSet<string>();

        var registry = PlayerPrefs.GetString(RegistryPrefsKey, "");
        if (!string.IsNullOrEmpty(registry))
        {
            var parts = registry.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
                keysToDelete.Add(parts[i]);
        }

        foreach (var key in keysToDelete)
            PlayerPrefs.DeleteKey(key);

        PlayerPrefs.DeleteKey(RegistryPrefsKey);
        PlayerPrefs.Save();
    }

    static string BuildStorageKey(string phaseId) => PlayerPrefsKeyPrefix + phaseId.Trim();

    static void RegisterStorageKey(string storageKey)
    {
        if (string.IsNullOrEmpty(storageKey))
            return;

        var registry = PlayerPrefs.GetString(RegistryPrefsKey, "");
        if (string.IsNullOrEmpty(registry))
        {
            PlayerPrefs.SetString(RegistryPrefsKey, storageKey);
            return;
        }

        var parts = registry.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] == storageKey)
                return;
        }

        PlayerPrefs.SetString(RegistryPrefsKey, registry + "\n" + storageKey);
    }
}
