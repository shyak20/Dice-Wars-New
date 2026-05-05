using System;
using UnityEngine;

/// <summary>
/// Activates this <see cref="GameObject"/> only until the player has "consumed" a first encounter
/// for the configured <see cref="persistence"/> mode. State is stored in <see cref="PlayerPrefs"/>.
/// The GameObject should start active in the scene so <see cref="Awake"/> runs.
/// </summary>
public class FirstEncounterDayVisibility : MonoBehaviour
{
    public const string PlayerPrefsKeyPrefix = "DiceWars_FirstEncounter_";

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

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
        {
            Debug.LogError(
                $"{nameof(FirstEncounterDayVisibility)} on '{name}': assign a non-empty {nameof(uniqueKey)}.",
                this);
            return;
        }

        if (IsAlreadyConsumed())
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

    private bool IsAlreadyConsumed()
    {
        var key = BuildStorageKey();
        switch (persistence)
        {
            case PersistenceMode.OnceEver:
                return PlayerPrefs.GetInt(key, 0) != 0;
            case PersistenceMode.OncePerLocalCalendarDay:
                var today = LocalCalendarDayString();
                return PlayerPrefs.GetString(key, "") == today;
            default:
                throw new ArgumentOutOfRangeException(nameof(persistence), persistence, null);
        }
    }

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

        PlayerPrefs.Save();
    }

    /// <summary>Clears stored progress for <paramref name="uniqueKey"/> and <paramref name="mode"/>.</summary>
    public static void ResetProgress(string uniqueKey, PersistenceMode mode)
    {
        if (string.IsNullOrWhiteSpace(uniqueKey))
            return;

        var key = BuildStorageKeyStatic(uniqueKey, mode);
        switch (mode)
        {
            case PersistenceMode.OnceEver:
                PlayerPrefs.DeleteKey(key);
                break;
            case PersistenceMode.OncePerLocalCalendarDay:
                PlayerPrefs.DeleteKey(key);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        PlayerPrefs.Save();
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
