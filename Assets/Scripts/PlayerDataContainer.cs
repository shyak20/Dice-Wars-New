using System.Collections.Generic;
using UnityEngine;

public class PlayerDataContainer : MonoBehaviour
{
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
    }
}
