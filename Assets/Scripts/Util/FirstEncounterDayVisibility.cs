using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Activates this <see cref="GameObject"/> only until the player has "consumed" a first encounter
/// for the configured <see cref="persistence"/> mode. State is stored in <see cref="PlayerPrefs"/>.
/// The GameObject should start active in the scene so <see cref="Awake"/> runs.
/// </summary>
public class FirstEncounterDayVisibility : MonoBehaviour
{
    public const string PlayerPrefsKeyPrefix = "DiceWars_FirstEncounter_";
    const string RegistryPrefsKey = PlayerPrefsKeyPrefix + "_Registry";

    /// <summary>
    /// Known encounter ids authored in scenes. Cleared on progression reset even if those scenes are not loaded.
    /// </summary>
    static readonly string[] KnownUniqueKeys =
    {
        "Tutorial Fight Scene",
        "Alpha opening Screen",
    };

    public enum PersistenceMode
    {
        /// <summary>After the first consumption, never show again (same install / prefs).</summary>
        OnceEver,
        /// <summary>After the first consumption on a given local calendar day, hide until the next calendar day.</summary>
        OncePerLocalCalendarDay,
    }

    [Tooltip("Stable id for PlayerPrefs (e.g. MainMenu_TutorialBanner). Must be unique per gated object or group.")]
    [SerializeField] private string uniqueKey;

    [SerializeField] private PersistenceMode persistence = PersistenceMode.OncePerLocalCalendarDay;

    [Tooltip("When true, consumption is recorded at the end of the first OnEnable where the object stays visible.")]
    [SerializeField] private bool consumeOnEnable = true;

    /// <summary>
    /// True when prefs already marked this encounter consumed before this Awake.
    /// Distinct from <see cref="IsConsumed"/> after <see cref="consumeOnEnable"/> runs in the same session.
    /// </summary>
    public bool WasConsumedPriorToThisLoad { get; private set; }

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
        {
            Debug.LogError(
                $"{nameof(FirstEncounterDayVisibility)} on '{name}': assign a non-empty {nameof(uniqueKey)}.",
                this);
            return;
        }

        WasConsumedPriorToThisLoad = IsAlreadyConsumed();
        if (WasConsumedPriorToThisLoad)
            gameObject.SetActive(false);
        else
            gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
            return;

        if (!gameObject.activeInHierarchy)
            return;

        if (IsAlreadyConsumed())
            return;

        if (consumeOnEnable)
            Consume();
    }

    /// <summary>True when this encounter id has already been consumed for <paramref name="mode"/>.</summary>
    public static bool IsConsumed(string uniqueKey, PersistenceMode mode)
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
            return false;

        var key = BuildStorageKeyStatic(uniqueKey.Trim(), mode);
        switch (mode)
        {
            case PersistenceMode.OnceEver:
                return PlayerPrefs.GetInt(key, 0) != 0;
            case PersistenceMode.OncePerLocalCalendarDay:
                return PlayerPrefs.GetString(key, "") == LocalCalendarDayString();
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    /// <summary>True when this component's configured encounter has already been consumed.</summary>
    public bool IsConsumed()
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
            return false;
        return IsConsumed(uniqueKey.Trim(), persistence);
    }

    private bool IsAlreadyConsumed() => IsConsumed();

    /// <summary>Marks this encounter as done so the object stays hidden per <see cref="persistence"/>.</summary>
    public void Consume()
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
        {
            Debug.LogError(
                $"{nameof(FirstEncounterDayVisibility)} on '{name}': assign {nameof(uniqueKey)} before Consume.",
                this);
            return;
        }

        var key = BuildStorageKey();
        switch (persistence)
        {
            case PersistenceMode.OnceEver:
                PlayerPrefs.SetInt(key, 1);
                break;
            case PersistenceMode.OncePerLocalCalendarDay:
                PlayerPrefs.SetString(key, LocalCalendarDayString());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(persistence), persistence, null);
        }

        RegisterStorageKey(key);
        PlayerPrefs.Save();
    }

    /// <summary>Clears stored progress for <paramref name="uniqueKey"/> and <paramref name="mode"/>.</summary>
    public static void ResetProgress(string uniqueKey, PersistenceMode mode)
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
            return;

        var key = BuildStorageKeyStatic(uniqueKey, mode);
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Clears all first-encounter PlayerPrefs (tutorials, opening screens, etc.).
    /// Called when progression / player save is wiped so tutorials can play again.
    /// </summary>
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

        for (var i = 0; i < KnownUniqueKeys.Length; i++)
            AddBothModeKeys(keysToDelete, KnownUniqueKeys[i]);

        for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            var roots = scene.GetRootGameObjects();
            for (var r = 0; r < roots.Length; r++)
            {
                var components = roots[r].GetComponentsInChildren<FirstEncounterDayVisibility>(true);
                for (var c = 0; c < components.Length; c++)
                {
                    var encounter = components[c];
                    if (encounter == null || string.IsNullOrWhiteSpace(encounter.uniqueKey))
                        continue;
                    AddBothModeKeys(keysToDelete, encounter.uniqueKey.Trim());
                }
            }
        }

        foreach (var key in keysToDelete)
            PlayerPrefs.DeleteKey(key);

        PlayerPrefs.DeleteKey(RegistryPrefsKey);
        PlayerPrefs.Save();
    }

    static void AddBothModeKeys(HashSet<string> keys, string uniqueKey)
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
            return;
        keys.Add(BuildStorageKeyStatic(uniqueKey, PersistenceMode.OnceEver));
        keys.Add(BuildStorageKeyStatic(uniqueKey, PersistenceMode.OncePerLocalCalendarDay));
    }

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

    private string BuildStorageKey() => BuildStorageKeyStatic(uniqueKey, persistence);

    private static string BuildStorageKeyStatic(string id, PersistenceMode mode)
    {
        return mode switch
        {
            PersistenceMode.OnceEver => PlayerPrefsKeyPrefix + id,
            PersistenceMode.OncePerLocalCalendarDay => PlayerPrefsKeyPrefix + id + "_CalDay",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static string LocalCalendarDayString() => DateTime.Now.ToString("yyyy-MM-dd");

#if UNITY_EDITOR
    [ContextMenu("Dev/Clear encounter progress (this component)")]
    private void EditorClearProgress()
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
        {
            Debug.LogWarning($"{nameof(FirstEncounterDayVisibility)}: no {nameof(uniqueKey)} to clear.", this);
            return;
        }

        ResetProgress(uniqueKey, persistence);
        Debug.Log(
            $"{nameof(FirstEncounterDayVisibility)}: cleared prefs for '{uniqueKey}' ({persistence}).",
            this);
    }
#endif
}
