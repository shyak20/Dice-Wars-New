using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-act content for map runs: which enemies and unknown events can appear on this act.
/// Combat tiles draw from <see cref="possibleEnemies"/> by matching <see cref="EnemyTypeSO.enemyRank"/>.
/// </summary>
[CreateAssetMenu(fileName = "MapActDefinition", menuName = "DiceGame/Map/Map Act Definition")]
public class MapActDefinitionSO : ScriptableObject
{
    [Tooltip("All enemies that can appear this act. Normal / elite / boss tiles filter by EnemyRank on each asset.")]
    public List<EnemyTypeSO> possibleEnemies = new List<EnemyTypeSO>();

    [Tooltip("Pool for Unknown tiles — drawn with unique-first logic like combat enemies.")]
    public List<UnknownMapEventSO> possibleUnknownEvents = new List<UnknownMapEventSO>();

    [Tooltip("Treasure tiles pick one pack at random when the player opens the chest.")]
    public List<MapTreasurePackSO> treasurePacks = new List<MapTreasurePackSO>();

    [Header("Map layout")]
    [Tooltip("Grid columns for maps generated this act (start at x=0, boss at width−1).")]
    [Min(1)] public int gridWidth = 4;
    [Tooltip("Grid rows for maps generated this act.")]
    [Min(1)] public int gridHeight = 4;
    [Tooltip("Moves allowed before corruption / overflow damage (see MapMovementManager).")]
    [Min(1)] public int moveLimit = 8;

    [Header("Map Events")]
    [Min(0)] public int eliteMinOnMap = 1;
    [Min(0)] public int eliteMaxOnMap = 2;
    [Tooltip("Forced shop tiles (count rolled between min and max). If both are 0, shop frequency uses only RunManager filler weights.")]
    [Min(0)] public int shopMinOnMap;
    [Min(0)] public int shopMaxOnMap;

    /// <summary>Non-allocating: clears <paramref name="into"/> then adds all non-null enemies matching <paramref name="rank"/>.</summary>
    public void CollectEnemiesForRank(EnemyRank rank, List<EnemyTypeSO> into)
    {
        into.Clear();
        if (possibleEnemies == null)
            return;
        foreach (var e in possibleEnemies)
        {
            if (e != null && e.enemyRank == rank)
                into.Add(e);
        }
    }

    private void OnValidate()
    {
        gridWidth = Mathf.Max(1, gridWidth);
        gridHeight = Mathf.Max(1, gridHeight);
        moveLimit = Mathf.Max(1, moveLimit);
        eliteMaxOnMap = Mathf.Max(eliteMaxOnMap, eliteMinOnMap);
        shopMaxOnMap = Mathf.Max(shopMaxOnMap, shopMinOnMap);
        WarnIfRankMissing(EnemyRank.Boss, "boss end tile");
        WarnIfRankMissing(EnemyRank.Normal, "normal combat tiles");
        WarnIfRankMissing(EnemyRank.Elite, "elite combat tiles");
    }

    private void WarnIfRankMissing(EnemyRank rank, string context)
    {
        if (possibleEnemies == null)
            return;
        foreach (var e in possibleEnemies)
        {
            if (e != null && e.enemyRank == rank)
                return;
        }
        Debug.LogWarning($"{name}: no {rank} enemy in possibleEnemies — {context} may fail at runtime.", this);
    }
}
