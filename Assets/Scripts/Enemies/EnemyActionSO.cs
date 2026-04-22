using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAction", menuName = "DiceGame/EnemyAction")]
public class EnemyActionSO : ScriptableObject
{
    public string actionName;

    [Tooltip("Shown when this intent has no game actions, or none of them define an icon.")]
    public Sprite icon;
    public int damage;
    public int numberOfAttacks = 1; // Default to 1 hit
    public int armor;

    [Header("Combat animator")]
    [Tooltip("State name on this enemy type's RuntimeAnimatorController (e.g. Attack or Base Layer.Swing). Empty keeps current pose (usually idle) for this intent.")]
    public string actionAnimatorStateName;

    [Tooltip("After starting the action state, wait this long before applying damage, armor, and game actions.")]
    [Min(0f)] public float actionAnimationLeadInSeconds = 0.2f;

    [Header("Game actions (same system as dice faces)")]
    [Tooltip("Optional. Executed after this intent's attacks and armor gain (same order as on a die face). Face-only modifiers are skipped.")]
    [SerializeReference] public List<IGameAction> actions = new List<IGameAction>();

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