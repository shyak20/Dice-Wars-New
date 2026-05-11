using UnityEngine;

/// <summary>
/// One possible outcome for an <see cref="MapEventType.Unknown"/> tile on a given act.
/// </summary>
[CreateAssetMenu(fileName = "UnknownMapEvent", menuName = "DiceGame/Map/Unknown Map Event")]
public class UnknownMapEventSO : ScriptableObject
{
    [Tooltip("Shown in logs/UI; defaults to asset name if empty.")]
    public string displayName;

    [TextArea(2, 6)]
    public string description;

    public Sprite icon;

    [Header("Combat (after player presses Continue on Unknown panel)")]
    [Tooltip("If true, closing the Unknown panel starts a fight using the enemy below.")]
    public bool triggersCombat;

    [Tooltip("Used when Specific Enemy is empty — same pool rules as normal map combat for this rank.")]
    public EnemyRank combatRank = EnemyRank.Normal;

    [Tooltip("If set, this exact enemy is used. If empty, one is drawn from the act pool for Combat Rank.")]
    public EnemyTypeSO specificEnemy;

    [Tooltip("If true, victory from this fight advances the act like a boss tile (RunEncounterBuffer boss flag).")]
    public bool countsAsBossTileForRunProgression;

    public string DisplayLabel => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>Enemy for this encounter: explicit asset or pooled draw for <see cref="combatRank"/>.</summary>
    public EnemyTypeSO ResolveEnemyForCombat(RunManager runManager)
    {
        if (specificEnemy != null)
            return specificEnemy;
        if (runManager == null)
        {
            Debug.LogError("UnknownMapEventSO.ResolveEnemyForCombat: RunManager is null.");
            return null;
        }

        return runManager.DrawEnemyForMapCombat(combatRank);
    }
}
