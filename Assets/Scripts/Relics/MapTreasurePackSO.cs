using System;
using System.Collections.Generic;
using UnityEngine;

public enum TreasureRewardKind
{
    Gold,
}

[Serializable]
public struct TreasureRewardEntry
{
    public TreasureRewardKind kind;
    [Min(0)] public int goldMin;
    [Min(0)] public int goldMax;
}

/// <summary>One treasure chest definition for map <see cref="MapEventType.Treasure"/> tiles; rolled per act from <see cref="MapActDefinitionSO"/>.</summary>
[CreateAssetMenu(fileName = "MapTreasurePack", menuName = "DiceGame/Map/Treasure Pack")]
public class MapTreasurePackSO : ScriptableObject
{
    public string packTitle;
    [TextArea] public string packDescription;
    public List<TreasureRewardEntry> rewards = new List<TreasureRewardEntry>();
    [Header("Chance-based bonus rewards")]
    [Range(0f, 1f)] public float dieDropChance;
    public DieLootTableSO dieDropLootTable;
    [Range(0f, 1f)] public float relicDropChance;
    public RelicLootTableSO relicDropLootTable;

    private void OnValidate()
    {
        dieDropChance = Mathf.Clamp01(dieDropChance);
        relicDropChance = Mathf.Clamp01(relicDropChance);
    }
}
