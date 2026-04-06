using System;
using UnityEngine;

/// <summary>Run-scoped gold (singleton, DontDestroyOnLoad). Meta currency during a run.</summary>
public class RunEconomyManager : MonoBehaviour
{
    public static RunEconomyManager Instance { get; private set; }

    [SerializeField] private int startingGold;

    [Header("Gold pop-up (optional)")]
    [SerializeField] private GoldPopupWorldSpawner goldPopupSpawner;

    public int CurrentGold { get; private set; }

    /// <summary>Invoked after any gold change (argument = new total).</summary>
    public static event Action<int> OnGoldChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentGold = startingGold;
        OnGoldChanged?.Invoke(CurrentGold);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetEconomyForNewRun(int gold = -1)
    {
        CurrentGold = gold >= 0 ? gold : startingGold;
        OnGoldChanged?.Invoke(CurrentGold);
    }

    public bool CanAfford(int amount) => amount >= 0 && CurrentGold >= amount;

    public bool TrySpend(int amount)
    {
        if (amount < 0 || CurrentGold < amount) return false;
        CurrentGold -= amount;
        OnGoldChanged?.Invoke(CurrentGold);
        return true;
    }

    /// <summary>Adds gold and optionally shows a floating pop-up at a world position (e.g. defeated enemy).</summary>
    public void GrantGold(int amount, Vector3? worldPopupPosition = null)
    {
        if (amount <= 0) return;
        CurrentGold += amount;
        OnGoldChanged?.Invoke(CurrentGold);

        if (worldPopupPosition.HasValue && goldPopupSpawner != null)
            goldPopupSpawner.Spawn(amount, worldPopupPosition.Value);
    }
}
