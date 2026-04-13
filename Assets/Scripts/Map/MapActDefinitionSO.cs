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

    [Header("Map layout (this act)")]
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
