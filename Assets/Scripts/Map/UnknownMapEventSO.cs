using System;
using System.Collections.Generic;
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

    [Tooltip("Art shown on the unknown event panel when set.")]
    public Sprite eventArt;

    [Header("Visibility (draw pool)")]
    [SerializeReference]
    [Tooltip("All must pass for this event to be drawable (AND). Add condition types via the + picker.")]
    public List<UnknownMapEventConditionBase> visibilityConditions = new List<UnknownMapEventConditionBase>();

    [Tooltip("When true, this row is removed from the draw pool for the rest of the run after this asset’s completion key is registered (see ResolvedEventId).")]
    public bool excludeFromDrawIfCompletedThisRun;

    [Header("Choices")]
    [Tooltip("If non-empty, the panel shows one button per row (when that row’s conditions pass). Legacy Continue-only flow is used when this is empty.")]
    public UnknownMapEventOptionEntry[] choices;

    [Header("Combat")]
    [Tooltip("When true, entering this Unknown tile starts a fight immediately (no event panel). Enemy comes from Specific Enemy, or is drawn from the act pool for Combat Rank.")]
    public bool triggersCombat;

    [Tooltip("Used when Specific Enemy is empty — same pool rules as normal map combat for this rank.")]
    public EnemyRank combatRank = EnemyRank.Normal;

    [Tooltip("If set, this exact enemy is used. If empty, one is drawn from the act pool for Combat Rank.")]
    public EnemyTypeSO specificEnemy;

    [Tooltip("If true, victory from this fight advances the act like a boss tile (RunEncounterBuffer boss flag).")]
    public bool countsAsBossTileForRunProgression;

    [Tooltip("When there are no choice rows and legacy combat runs on tile enter, register completion (Clear Event chains) using ResolvedEventId.")]
    public bool registerCompletedOnLegacyCombatEnter;

    public string DisplayLabel => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>Completion / chain key derived from this asset’s name (rename the asset file to change the id).</summary>
    public string ResolvedEventId =>
        string.IsNullOrWhiteSpace(name) ? "UnknownMapEvent" : name.Trim();

    public bool HasChoiceOptions => choices != null && choices.Length > 0;

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

    private void OnValidate()
    {
        if (choices != null && choices.Length > 0 && triggersCombat)
            Debug.LogWarning(
                $"{name}: triggersCombat is enabled — choice rows are ignored; combat starts on tile enter instead.",
                this);

        ValidateConditionList(visibilityConditions, "Visibility", this);
        if (choices != null)
        {
            for (var i = 0; i < choices.Length; i++)
            {
                var row = choices[i];
                if (row == null)
                {
                    Debug.LogError($"{name}: choices[{i}] is null.", this);
                    continue;
                }

                ValidateConditionList(row.enabledWhen, $"Choice '{row.label}'", this);
            }
        }
    }

    static void ValidateConditionList(IReadOnlyList<UnknownMapEventConditionBase> list, string listLabel, UnityEngine.Object context)
    {
        if (list == null)
            return;
        for (var i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (c == null)
            {
                Debug.LogError($"{listLabel}: condition entry [{i}] is null.", context);
                continue;
            }

            if (c is UnknownMapEventConditionCompletedUnknownEvent done)
            {
                if (done.requiredCompletedUnknownEvent == null)
                    Debug.LogError(
                        $"{listLabel}: Completed Unknown Event condition [{i}] has no Unknown Map Event assigned.",
                        context);
                else if (context is UnknownMapEventSO self && done.requiredCompletedUnknownEvent == self)
                    Debug.LogError(
                        $"{listLabel}: Completed Unknown Event condition [{i}] references this same event — use another asset.",
                        context);
            }
        }
    }
}

/// <summary>One button row on the unknown event panel.</summary>
[Serializable]
public class UnknownMapEventOptionEntry
{
    [Tooltip("Button label.")]
    public string label;

    [SerializeReference]
    [Tooltip("All must pass for this option to be shown (AND). Add condition types via the + picker.")]
    public List<UnknownMapEventConditionBase> enabledWhen = new List<UnknownMapEventConditionBase>();

    [Tooltip("If true, picking this option registers the parent event’s completion key (ResolvedEventId) after outcomes run.")]
    public bool registerEventCompletedOnPick = true;

    [SerializeReference]
    [Tooltip("Effects when this option is chosen (gold, combat, composite, …).")]
    public UnknownMapEventOutcomeBase outcome;
}
