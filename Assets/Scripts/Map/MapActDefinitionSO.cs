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

    [Header("Map Events (min/max per map, excluding start & boss)")]
    [Tooltip("Elite combat tiles — not orthogonally adjacent to each other.")]
    [Min(0)] public int eliteMinOnMap = 1;
    [Min(0)] public int eliteMaxOnMap = 2;

    [Tooltip("Shop tiles. Min=max=0 disables shops for structured maps. If Shrine/Unknown/Treasure min/max are all 0, fillers use RunManager weights (legacy). Otherwise each type rolls in [min,max]; leftover fillers are normal combat.")]
    [Min(0)] public int shopMinOnMap;
    [Min(0)] public int shopMaxOnMap;

    [Min(0)] public int shrineMinOnMap;
    [Min(0)] public int shrineMaxOnMap;

    [Min(0)] public int unknownMinOnMap;
    [Min(0)] public int unknownMaxOnMap;

    [Min(0)] public int treasureMinOnMap;
    [Min(0)] public int treasureMaxOnMap;

    [Header("Min path distance from start (directed steps)")]
    [Tooltip("Elite tiles must be at least this many moves from the start along map exits (0 = no minimum).")]
    [Min(0)] public int eliteMinStepsFromStart;

    [Tooltip("Shop tiles minimum steps from start.")]
    [Min(0)] public int shopMinStepsFromStart;

    [Tooltip("Shrine tiles minimum steps from start.")]
    [Min(0)] public int shrineMinStepsFromStart;

    [Tooltip("Treasure tiles minimum steps from start.")]
    [Min(0)] public int treasureMinStepsFromStart;

    [Tooltip("Unknown tiles minimum steps from start.")]
    [Min(0)] public int unknownMinStepsFromStart;

    [Header("Deck building")]
    [Tooltip("Max faces on a single die that may share the same numeric value (DieFaceSO.value). If the die already has this many copies of a value, a new face with that value may only replace one of those slots — not a different value.")]
    [Min(1)] public int maxSameNumericValueFacesPerDie = 6;

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
        shrineMaxOnMap = Mathf.Max(shrineMaxOnMap, shrineMinOnMap);
        unknownMaxOnMap = Mathf.Max(unknownMaxOnMap, unknownMinOnMap);
        treasureMaxOnMap = Mathf.Max(treasureMaxOnMap, treasureMinOnMap);
        eliteMinStepsFromStart = Mathf.Max(0, eliteMinStepsFromStart);
        shopMinStepsFromStart = Mathf.Max(0, shopMinStepsFromStart);
        shrineMinStepsFromStart = Mathf.Max(0, shrineMinStepsFromStart);
        treasureMinStepsFromStart = Mathf.Max(0, treasureMinStepsFromStart);
        unknownMinStepsFromStart = Mathf.Max(0, unknownMinStepsFromStart);
        maxSameNumericValueFacesPerDie = Mathf.Max(1, maxSameNumericValueFacesPerDie);
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
