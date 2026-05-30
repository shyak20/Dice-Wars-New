using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewAction", menuName = "DiceGame/EnemyAction")]
public class EnemyActionSO : ScriptableObject
{
    public string actionName;

    [Header("Intent tooltips (TMP styles, e.g. <style=Attack>4</style>)")]
    [Tooltip("Hover title on the physical attack intent row.")]
    public string attackTooltipTitle;
    [TextArea(2, 4)]
    public string attackTooltipDescription;
    [Tooltip("Hover title on the armor intent row.")]
    public string armorTooltipTitle;
    [TextArea(2, 4)]
    public string armorTooltipDescription;

    [SerializeField, HideInInspector, FormerlySerializedAs("actionDescription")]
    private string legacyActionDescription;

    [Serializable]
    public class ActionTooltipEntry
    {
        public string title;
        [TextArea(2, 4)] public string description;
    }

    [Tooltip("Shown when this intent has no game actions, or none of them define an icon.")]
    public Sprite icon;
    public int damage;
    public int numberOfAttacks = 1; // Default to 1 hit
    public int armor;

    [Header("Combat animator")]
    [Tooltip("Trigger parameter name on this enemy type's RuntimeAnimatorController (e.g. AttackTrigger). Empty keeps current pose (usually idle) for this intent.")]
    [FormerlySerializedAs("actionAnimatorStateName")]
    public string actionAnimatorTriggerName;

    [Tooltip("After starting the action state, wait this long before applying damage, armor, and game actions.")]
    [Min(0f)] public float actionAnimationLeadInSeconds = 0.2f;

    [Header("Game actions (same system as dice faces)")]
    [Tooltip("Optional. Executed after this intent's attacks and armor gain (same order as on a die face). Face-only modifiers are skipped.")]
    [SerializeReference] public List<IGameAction> actions = new List<IGameAction>();
    [Tooltip("Per-action tooltip text by action index. Entry 0 maps to actions[0], etc.")]
    public List<ActionTooltipEntry> actionTooltips = new List<ActionTooltipEntry>();

    void OnValidate() => MigrateLegacyActionDescription();

    public void MigrateLegacyActionDescription()
    {
        if (string.IsNullOrWhiteSpace(legacyActionDescription))
            return;

        var legacy = legacyActionDescription.Trim();
        if (damage > 0 && string.IsNullOrWhiteSpace(attackTooltipDescription))
            attackTooltipDescription = legacy;
        else if (armor > 0 && string.IsNullOrWhiteSpace(armorTooltipDescription))
            armorTooltipDescription = legacy;

        legacyActionDescription = null;
    }

    /// <summary>
    /// Intent portrait: first non-null icon from <see cref="actions"/> (same rules as dice faces).
    /// Uses <see cref="icon"/> when there are no actions, or when no action supplies an icon.
    /// </summary>
    public Sprite ResolveDisplayIcon()
    {
        if (actions != null && actions.Count > 0)
        {
            foreach (var a in actions)
            {
                if (a == null) continue;
                var s = GameActionIconUtility.GetDisplayIcon(a);
                if (s != null)
                    return s;
            }
        }

        return icon;
    }
}