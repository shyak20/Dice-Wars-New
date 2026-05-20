using System;
using UnityEngine;

/// <summary>
/// Account-wide meta progression persisted via <see cref="PlayerPrefs"/> (survives runs and app restarts).
/// </summary>
public sealed class MetaProgressionManager : MonoBehaviour
{
    public const string RubyShardsPlayerPrefsKey = "DiceWars_Meta_RubyShards";

    public static MetaProgressionManager Instance { get; private set; }

    /// <summary>Resolves the manager for grant/display paths. Creates a DontDestroyOnLoad fallback if missing.</summary>
    public static MetaProgressionManager TryGetRuntime()
    {
        if (Instance != null)
            return Instance;

        var found = FindObjectOfType<MetaProgressionManager>(true);
        if (found != null)
            return found;

        Debug.LogWarning(
            "MetaProgressionManager: No manager in scene — creating a runtime fallback. " +
            "Add MetaProgressionManager next to RunManager in your bootstrap scene.");

        var go = new GameObject(nameof(MetaProgressionManager));
        return go.AddComponent<MetaProgressionManager>();
    }

    public int CurrentRubyShards { get; private set; }

    public static event Action<int> OnRubyShardsChanged;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentRubyShards = Mathf.Max(0, PlayerPrefs.GetInt(RubyShardsPlayerPrefsKey, 0));
        OnRubyShardsChanged?.Invoke(CurrentRubyShards);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool CanAffordRubyShards(int amount) => amount >= 0 && CurrentRubyShards >= amount;

    public bool TrySpendRubyShards(int amount)
    {
        if (amount < 0 || CurrentRubyShards < amount)
            return false;

        CurrentRubyShards -= amount;
        PersistRubyShards();
        OnRubyShardsChanged?.Invoke(CurrentRubyShards);
        return true;
    }

    public void GrantRubyShards(int amount)
    {
        if (amount <= 0)
            return;

        CurrentRubyShards += amount;
        PersistRubyShards();
        OnRubyShardsChanged?.Invoke(CurrentRubyShards);
    }

    void PersistRubyShards()
    {
        PlayerPrefs.SetInt(RubyShardsPlayerPrefsKey, CurrentRubyShards);
        PlayerPrefs.Save();
    }
}
