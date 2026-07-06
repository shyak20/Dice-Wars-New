using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewFace", menuName = "DiceGame/DieFace")]
public class DieFaceSO : ScriptableObject
{
    [SerializeField] private string title;
    [SerializeField, HideInInspector] private string description; // Legacy single-line description (migration fallback).
    [Tooltip("Tooltip / card copy. Use {0} for a live value supplied by a face action (e.g. AddDamageFromDeckPipsModifier).")]
    [SerializeField] private List<string> descriptionLines = new List<string>();

    public string Title => string.IsNullOrEmpty(title) ? name : title;

    /// <summary>Description with live combat values when available.</summary>
    public string Description => BuildDescription(null);

    /// <summary>Description using an explicit combat context (e.g. hover tooltips during a fight).</summary>
    public string GetDescription(DieFaceDescriptionContext context) => BuildDescription(context);

    string BuildDescription(DieFaceDescriptionContext context)
    {
        if (descriptionLines != null && descriptionLines.Count > 0)
        {
            var nonEmptyLines = new List<string>();
            for (var i = 0; i < descriptionLines.Count; i++)
            {
                var line = descriptionLines[i];
                if (!string.IsNullOrWhiteSpace(line))
                    nonEmptyLines.Add(DieFaceDescriptionUtility.FormatDescription(this, line.Trim(), context));
            }

            if (nonEmptyLines.Count > 0)
                return string.Join("\n", nonEmptyLines);
        }

        return DieFaceDescriptionUtility.FormatDescription(this, LegacyDescriptionFallback, context);
    }

    public int value; // Keeping this for the Power Bar calculation
    public DieType type;

    /// <summary>Socketing element (maps from <see cref="type"/>). Curse faces match any die via <see cref="ElementTypeExtensions.MatchesDie"/>.</summary>
    public ElementType Element => ElementTypeExtensions.FromDieType(type);
    public Material faceMaterial;
    public FaceRarity rarity;

    [Header("Values")]
    public int damage; // New independent damage value
    [Tooltip("For attack faces only: pending physical uses (final damage after Strength/modifiers) × this many hits. Ignored when type is not Damage.")]
    [Min(1)]
    public int damageAttackTimes = 1;
    [Min(0)]
    public int armor;  // New independent armor value
    [Tooltip("HP damage you take when this turn is submitted (shown in the player element container flyout).")]
    [Min(0)]
    public int selfDamage;

    [Header("UI (card/picker/shop)")]
    public Sprite uiIcon;
    public Sprite uiTooltipBackground;

    [Header("Targeting")]
    [Tooltip("When enabled, every enemy-targeted effect on this face (damage and enemy debuffs) applies to all alive enemies.")]
    [SerializeField] private bool attackAllEnemies;

    /// <summary>When true, damage and enemy-targeted actions hit every alive enemy (see multi-enemy flyout auto-assign).</summary>
    public bool AttackAllEnemies => attackAllEnemies;

    [Header("Game Actions")]
    [Tooltip("Executed in list order. Use + in the inspector to add multiple polymorphic actions. Timing is per action (Activate Immediately on each action).")]
    [SerializeReference] public List<IGameAction> actions = new List<IGameAction>();

    internal string LegacyDescriptionFallback =>
        string.IsNullOrEmpty(description) ? name : description;
}