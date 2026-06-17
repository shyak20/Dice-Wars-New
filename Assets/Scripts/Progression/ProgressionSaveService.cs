using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Per-character progression stored in <see cref="PlayerPrefs"/> (obfuscated), not loose JSON under persistentDataPath.
/// Legacy JSON files are imported once on load, then removed.
/// </summary>
public static class ProgressionSaveService
{
    const int CurrentSaveVersion = 6;
    const string SaveKeyPrefix = "DiceWars_Progression_";
    const string SaveIndexKey = "DiceWars_Progression_Index";
    const string LegacyFolderName = "Progression";

    static readonly byte[] ObfuscationKey = BuildObfuscationKey();

    public static ProgressionProfileSaveData Load(string metaSaveId)
    {
        if (ProgressionContentIds.IsNullOrEmpty(metaSaveId))
            return NewProfile();

        var fromLegacyFile = TryImportLegacyJsonFile(metaSaveId);
        if (fromLegacyFile != null)
            return fromLegacyFile;

        var key = BuildSaveKey(metaSaveId);
        if (!PlayerPrefs.HasKey(key))
            return NewProfile();

        try
        {
            var encoded = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(encoded))
                return NewProfile();

            var json = Decode(encoded);
            if (string.IsNullOrWhiteSpace(json))
                return NewProfile();

            var data = JsonUtility.FromJson<ProgressionProfileSaveData>(json);
            if (data == null)
                return NewProfile();

            Normalize(data);
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ProgressionSaveService: failed to load '{metaSaveId}' from PlayerPrefs: {ex.Message}");
            return NewProfile();
        }
    }

    public static void Save(string metaSaveId, ProgressionProfileSaveData data)
    {
        if (ProgressionContentIds.IsNullOrEmpty(metaSaveId) || data == null)
            return;

        data.saveVersion = CurrentSaveVersion;
        Normalize(data);

        var json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(BuildSaveKey(metaSaveId), Encode(json));
        RegisterSaveId(metaSaveId);
        PlayerPrefs.Save();
    }

    public static void Delete(string metaSaveId)
    {
        if (ProgressionContentIds.IsNullOrEmpty(metaSaveId))
            return;

        PlayerPrefs.DeleteKey(BuildSaveKey(metaSaveId));
        UnregisterSaveId(metaSaveId);
        DeleteLegacyJsonFile(metaSaveId);
        PlayerPrefs.Save();
    }

    /// <summary>Removes all progression PlayerPrefs keys and any legacy JSON files.</summary>
    public static void DeleteAll()
    {
        DeleteAllIndexedSaves();
        DeleteAllLegacyJsonFiles();
        PlayerPrefs.Save();
    }

    public static ProgressionProfileSaveData NewProfile() =>
        new ProgressionProfileSaveData { saveVersion = CurrentSaveVersion };

    static void Normalize(ProgressionProfileSaveData data)
    {
        data.activeTrialStates ??= new List<TrialSaveData>();
        data.lifetimeTrialCounters ??= new List<TrialTypeLifetimeCounter>();
        data.unlockedContentIDs ??= new List<string>();
        data.completedTrialIDs ??= new List<string>();

        data.unlockedContentIDs = Dedupe(data.unlockedContentIDs);
        data.grantedStartingDice ??= new List<GrantedStartingDieSaveEntry>();
        data.completedTrialIDs = Dedupe(data.completedTrialIDs);
        data.unacknowledgedTrialIds = Dedupe(data.unacknowledgedTrialIds);
        data.currentRankIndex = Mathf.Max(0, data.currentRankIndex);
        MigrateLegacyGrantedStartingDice(data);
    }

    static void MigrateLegacyGrantedStartingDice(ProgressionProfileSaveData data)
    {
        if (data.addedStartingDieIds == null || data.addedStartingDieIds.Count == 0)
            return;

        if (data.grantedStartingDice.Count > 0)
            return;

        for (var i = 0; i < data.addedStartingDieIds.Count; i++)
        {
            var id = data.addedStartingDieIds[i];
            if (ProgressionContentIds.IsNullOrEmpty(id))
                continue;

            data.grantedStartingDice.Add(new GrantedStartingDieSaveEntry
            {
                dieAssetId = id,
                dieType = DieType.Damage
            });
        }
    }

    static List<string> Dedupe(List<string> ids)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i]?.Trim();
            if (ProgressionContentIds.IsNullOrEmpty(id) || !set.Add(id))
                continue;
            list.Add(id);
        }

        return list;
    }

    static string BuildSaveKey(string metaSaveId) => SaveKeyPrefix + SanitizeKeyPart(metaSaveId);

    static string SanitizeKeyPart(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "default";

        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        return sb.ToString();
    }

    static void RegisterSaveId(string metaSaveId)
    {
        var id = SanitizeKeyPart(metaSaveId);
        var index = PlayerPrefs.GetString(SaveIndexKey, string.Empty);
        if (string.IsNullOrEmpty(index))
        {
            PlayerPrefs.SetString(SaveIndexKey, id);
            return;
        }

        var parts = index.Split(',');
        for (var i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], id, StringComparison.Ordinal))
                return;
        }

        PlayerPrefs.SetString(SaveIndexKey, index + "," + id);
    }

    static void UnregisterSaveId(string metaSaveId)
    {
        var id = SanitizeKeyPart(metaSaveId);
        var index = PlayerPrefs.GetString(SaveIndexKey, string.Empty);
        if (string.IsNullOrEmpty(index))
            return;

        var parts = index.Split(',');
        var list = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i]?.Trim();
            if (string.IsNullOrEmpty(part) || string.Equals(part, id, StringComparison.Ordinal))
                continue;
            list.Add(part);
        }

        if (list.Count == 0)
            PlayerPrefs.DeleteKey(SaveIndexKey);
        else
            PlayerPrefs.SetString(SaveIndexKey, string.Join(",", list));
    }

    static void DeleteAllIndexedSaves()
    {
        var index = PlayerPrefs.GetString(SaveIndexKey, string.Empty);
        if (!string.IsNullOrEmpty(index))
        {
            var parts = index.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i]?.Trim();
                if (string.IsNullOrEmpty(part))
                    continue;
                PlayerPrefs.DeleteKey(SaveKeyPrefix + part);
            }
        }

        PlayerPrefs.DeleteKey(SaveIndexKey);
    }

    static ProgressionProfileSaveData TryImportLegacyJsonFile(string metaSaveId)
    {
        var path = GetLegacyJsonPath(metaSaveId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                File.Delete(path);
                return null;
            }

            var data = JsonUtility.FromJson<ProgressionProfileSaveData>(json);
            if (data == null)
            {
                File.Delete(path);
                return null;
            }

            Normalize(data);
            Save(metaSaveId, data);
            File.Delete(path);
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ProgressionSaveService: legacy import failed for '{metaSaveId}': {ex.Message}");
            return null;
        }
    }

    static void DeleteLegacyJsonFile(string metaSaveId)
    {
        var path = GetLegacyJsonPath(metaSaveId);
        if (File.Exists(path))
            File.Delete(path);
    }

    static void DeleteAllLegacyJsonFiles()
    {
        var dir = GetLegacyDirectory();
        if (!Directory.Exists(dir))
            return;

        foreach (var path in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ProgressionSaveService: failed to delete legacy '{path}': {ex.Message}");
            }
        }
    }

    static string GetLegacyDirectory() => Path.Combine(Application.persistentDataPath, LegacyFolderName);

    static string GetLegacyJsonPath(string metaSaveId) =>
        Path.Combine(GetLegacyDirectory(), SanitizeKeyPart(metaSaveId) + ".json");

    static byte[] BuildObfuscationKey()
    {
        var seed = Application.identifier + "|" + Application.companyName + "|" + SaveKeyPrefix;
        var hash = seed.GetHashCode();
        var bytes = new byte[16];
        for (var i = 0; i < bytes.Length; i++)
        {
            hash = unchecked(hash * 31 + i);
            bytes[i] = (byte)(hash ^ (hash >> 8) ^ (hash >> 16) ^ (hash >> 24));
        }

        return bytes;
    }

    static string Encode(string plain)
    {
        var bytes = Encoding.UTF8.GetBytes(plain);
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] ^= ObfuscationKey[i % ObfuscationKey.Length];
        return Convert.ToBase64String(bytes);
    }

    static string Decode(string encoded)
    {
        var bytes = Convert.FromBase64String(encoded);
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] ^= ObfuscationKey[i % ObfuscationKey.Length];
        return Encoding.UTF8.GetString(bytes);
    }
}
