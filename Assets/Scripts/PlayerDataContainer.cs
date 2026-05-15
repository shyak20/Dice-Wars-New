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

        CloneDeckForRuntime();
    }

    /// <summary>Runtime clone of <paramref name="template"/> is appended to the deck (e.g. shop purchase).</summary>
    public void AddDieToDeck(DieAssetSO template)
    {
        if (template == null)
        {
            Debug.LogError("PlayerDataContainer.AddDieToDeck: template is null.");
            return;
        }

        if (RuntimeData == null)
        {
            Debug.LogError("PlayerDataContainer.AddDieToDeck: RuntimeData is null.");
            return;
        }

        var clone = Instantiate(template);
        clone.name = template.name;
        RuntimeData.currentDeck.Add(clone);
        OnRuntimeDeckChanged?.Invoke();
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

    /// <summary>Duplicates one random non-null die in the deck (runtime <see cref="Object.Instantiate"/> clone).</summary>
    public bool TryDuplicateRandomDeckDie()
    {
        if (RuntimeData?.currentDeck == null)
        {
            Debug.LogError("PlayerDataContainer.TryDuplicateRandomDeckDie: RuntimeData or deck is null.");
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
        var src = RuntimeData.currentDeck[pick];
        if (src == null)
            return false;

        var copy = UnityEngine.Object.Instantiate(src);
        copy.name = src.name;
        RuntimeData.currentDeck.Add(copy);
        OnRuntimeDeckChanged?.Invoke();
        return true;
    }

    private void CloneDeckForRuntime()
    {
        RuntimeData = Instantiate(sourcePlayerData);
        for (var d = 0; d < RuntimeData.currentDeck.Count; d++)
        {
            var clonedDie = Instantiate(RuntimeData.currentDeck[d]);
            clonedDie.name = RuntimeData.currentDeck[d].name;
            RuntimeData.currentDeck[d] = clonedDie;
        }
    }

    /// <summary>Replaces the run deck when the player confirms starting dice in <see cref="DiceSelectSceneController"/>.</summary>
    public void ReplaceStartingDeck(IReadOnlyList<DieAssetSO> templates)
    {
        if (RuntimeData == null)
        {
            Debug.LogError("PlayerDataContainer.ReplaceStartingDeck: RuntimeData is null.");
            return;
        }

        if (templates == null || templates.Count == 0)
        {
            Debug.LogError("PlayerDataContainer.ReplaceStartingDeck: templates is null or empty.");
            return;
        }

        RuntimeData.currentDeck.Clear();
        foreach (var template in templates)
        {
            if (template == null)
            {
                Debug.LogError("PlayerDataContainer.ReplaceStartingDeck: null entry in templates list.");
                continue;
            }

            var clone = Instantiate(template);
            clone.name = template.name;
            RuntimeData.currentDeck.Add(clone);
        }

        OnRuntimeDeckChanged?.Invoke();
    }
}
