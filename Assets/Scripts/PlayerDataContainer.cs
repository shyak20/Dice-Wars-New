using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDataContainer : MonoBehaviour
{
    /// <summary>Fired after <see cref="RuntimeData"/> deck contents change (shop, treasure, dice select, etc.).</summary>
    public static event Action OnRuntimeDeckChanged;

    /// <summary>Raises <see cref="OnRuntimeDeckChanged"/> — events may only be invoked from inside this type.</summary>
    public static void NotifyRuntimeDeckChanged() => OnRuntimeDeckChanged?.Invoke();

    [SerializeField] private PlayerDataSO sourcePlayerData;

    public static PlayerDataContainer Instance { get; private set; }
    public PlayerDataSO RuntimeData { get; private set; }
    public PlayerDataSO ActiveCharacterTemplate { get; private set; }

    /// <summary>Most recent runtime die instance added by <see cref="AddDieToDeck"/> or duplicate helpers.</summary>
    public DieAssetSO LastAddedDeckDie { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sourcePlayerData == null)
        {
            Debug.LogError("PlayerDataContainer: sourcePlayerData is not assigned!");
            return;
        }

        ApplyCharacterProfile(sourcePlayerData);
    }

    /// <summary>
    /// Clones a character profile (deck + combat settings) into <see cref="RuntimeData"/> for the run.
    /// </summary>
    public void ApplyCharacterProfile(PlayerDataSO profile)
    {
        if (profile == null)
        {
            Debug.LogError("PlayerDataContainer.ApplyCharacterProfile: profile is null.");
            return;
        }

        DestroyRuntimeDeckInstances();
        if (RuntimeData != null)
            Destroy(RuntimeData);

        ActiveCharacterTemplate = profile;
        RuntimeData = Instantiate(profile);
        RuntimeData.name = profile.name;

        if (RuntimeData.currentDeck == null)
            RuntimeData.currentDeck = new List<DieAssetSO>();

        var templateDeck = profile.currentDeck ?? new List<DieAssetSO>();
        RuntimeData.currentDeck.Clear();
        for (var d = 0; d < templateDeck.Count; d++)
        {
            var templateDie = templateDeck[d];
            if (templateDie == null)
            {
                Debug.LogError($"PlayerDataContainer.ApplyCharacterProfile: '{profile.name}' deck slot {d} is null.");
                continue;
            }

            var clonedDie = Instantiate(templateDie);
            clonedDie.name = templateDie.name;
            RuntimeData.currentDeck.Add(clonedDie);
        }

        OnRuntimeDeckChanged?.Invoke();
    }

    void DestroyRuntimeDeckInstances()
    {
        if (RuntimeData?.currentDeck == null)
            return;

        for (var i = 0; i < RuntimeData.currentDeck.Count; i++)
        {
            var die = RuntimeData.currentDeck[i];
            if (die != null)
                Destroy(die);
        }
    }

    /// <summary>Runtime clone of <paramref name="template"/> is appended to the deck (e.g. shop purchase).</summary>
    public DieAssetSO AddDieToDeck(DieAssetSO template)
    {
        LastAddedDeckDie = null;
        if (template == null)
        {
            Debug.LogError("PlayerDataContainer.AddDieToDeck: template is null.");
            return null;
        }

        if (RuntimeData == null)
        {
            Debug.LogError("PlayerDataContainer.AddDieToDeck: RuntimeData is null.");
            return null;
        }

        var clone = Instantiate(template);
        clone.name = template.name;
        RuntimeData.currentDeck.Add(clone);
        LastAddedDeckDie = clone;
        OnRuntimeDeckChanged?.Invoke();
        return clone;
    }

    /// <summary>Removes the first deck die whose <see cref="DieAssetSO.dieType"/> matches <paramref name="dieType"/>.</summary>
    public bool TryRemoveFirstDieOfTypeFromDeck(DieType dieType)
    {
        if (RuntimeData?.currentDeck == null)
        {
            Debug.LogError("PlayerDataContainer.TryRemoveFirstDieOfTypeFromDeck: RuntimeData or deck is null.");
            return false;
        }

        for (var i = 0; i < RuntimeData.currentDeck.Count; i++)
        {
            var d = RuntimeData.currentDeck[i];
            if (d == null || d.dieType != dieType)
                continue;
            Destroy(d);
            RuntimeData.currentDeck.RemoveAt(i);
            OnRuntimeDeckChanged?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>Removes a specific runtime deck die instance.</summary>
    public bool TryRemoveDieFromDeck(DieAssetSO die)
    {
        if (RuntimeData?.currentDeck == null)
        {
            Debug.LogError("PlayerDataContainer.TryRemoveDieFromDeck: RuntimeData or deck is null.");
            return false;
        }

        if (die == null)
            return false;

        var idx = RuntimeData.currentDeck.IndexOf(die);
        if (idx < 0)
            return false;

        Destroy(die);
        RuntimeData.currentDeck.RemoveAt(idx);
        OnRuntimeDeckChanged?.Invoke();
        return true;
    }

    /// <summary>Removes one random non-null die from the deck.</summary>
    public bool TryRemoveRandomDieFromDeck()
    {
        if (RuntimeData?.currentDeck == null)
        {
            Debug.LogError("PlayerDataContainer.TryRemoveRandomDieFromDeck: RuntimeData or deck is null.");
            return false;
        }

        var indices = new List<int>();
        for (var i = 0; i < RuntimeData.currentDeck.Count; i++)
        {
            if (RuntimeData.currentDeck[i] != null)
                indices.Add(i);
        }

        if (indices.Count == 0)
            return false;

        var pick = indices[UnityEngine.Random.Range(0, indices.Count)];
        var die = RuntimeData.currentDeck[pick];
        Destroy(die);
        RuntimeData.currentDeck.RemoveAt(pick);
        OnRuntimeDeckChanged?.Invoke();
        return true;
    }

    /// <summary>Duplicates a specific runtime deck die (runtime <see cref="Object.Instantiate"/> clone).</summary>
    public DieAssetSO TryDuplicateDeckDie(DieAssetSO die)
    {
        LastAddedDeckDie = null;
        if (RuntimeData?.currentDeck == null)
        {
            Debug.LogError("PlayerDataContainer.TryDuplicateDeckDie: RuntimeData or deck is null.");
            return null;
        }

        if (die == null || !RuntimeData.currentDeck.Contains(die))
            return null;

        var copy = UnityEngine.Object.Instantiate(die);
        copy.name = die.name;
        RuntimeData.currentDeck.Add(copy);
        LastAddedDeckDie = copy;
        OnRuntimeDeckChanged?.Invoke();
        return copy;
    }

    /// <summary>Duplicates one random non-null die in the deck (runtime <see cref="Object.Instantiate"/> clone).</summary>
    public DieAssetSO TryDuplicateRandomDeckDie()
    {
        LastAddedDeckDie = null;
        if (RuntimeData?.currentDeck == null)
        {
            Debug.LogError("PlayerDataContainer.TryDuplicateRandomDeckDie: RuntimeData or deck is null.");
            return null;
        }

        var indices = new List<int>();
        for (var i = 0; i < RuntimeData.currentDeck.Count; i++)
        {
            if (RuntimeData.currentDeck[i] != null)
                indices.Add(i);
        }

        if (indices.Count == 0)
            return null;

        var pick = indices[UnityEngine.Random.Range(0, indices.Count)];
        var src = RuntimeData.currentDeck[pick];
        if (src == null)
            return null;

        var copy = UnityEngine.Object.Instantiate(src);
        copy.name = src.name;
        RuntimeData.currentDeck.Add(copy);
        LastAddedDeckDie = copy;
        OnRuntimeDeckChanged?.Invoke();
        return copy;
    }
}
