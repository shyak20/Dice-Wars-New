using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project-wide visuals for progression reward rows. Stat and per-element die-unlock icons
/// come from <see cref="iconIndex"/>; optional fallbacks cover missing item sprites.
/// </summary>
[CreateAssetMenu(fileName = "ProgressionRewardVisualCatalog", menuName = "DiceGame/Progression/Reward Visual Catalog")]
public sealed class ProgressionRewardVisualCatalogSO : ScriptableObject
{
    [SerializeField] private GameIconIndexSO iconIndex;

    [SerializeField] private List<ProgressionRewardVisualFallback> fallbackSprites = new List<ProgressionRewardVisualFallback>();

    public GameIconIndexSO IconIndex => iconIndex;

    public Sprite GetFallbackSprite(ProgressionRewardVisualKind kind)
    {
        for (var i = 0; i < fallbackSprites.Count; i++)
        {
            var entry = fallbackSprites[i];
            if (entry.kind == kind)
                return entry.sprite;
        }

        return null;
    }

    void OnValidate()
    {
        if (iconIndex == null)
        {
            Debug.LogWarning(
                $"ProgressionRewardVisualCatalogSO '{name}': assign iconIndex (e.g. Assets/Data/GameIconIndex.asset).",
                this);
        }
    }

    [Serializable]
    public struct ProgressionRewardVisualFallback
    {
        public ProgressionRewardVisualKind kind;
        public Sprite sprite;
    }
}
