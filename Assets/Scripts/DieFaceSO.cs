using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewFace", menuName = "DiceGame/DieFace")]
public class DieFaceSO : ScriptableObject
{
    [SerializeField] private string title;
    [SerializeField, HideInInspector] private string description; // Legacy single-line description (migration fallback).
    [SerializeField] private List<string> descriptionLines = new List<string>();

    public string Title => string.IsNullOrEmpty(title) ? name : title;
    public string Description => BuildDescription();

    public int value; // Keeping this for the Power Bar calculation
    public DieType type;

    /// <summary>Socketing element (maps from <see cref="type"/>; Physical = Damage, Defense = Armor).</summary>
    public ElementType Element => ElementTypeExtensions.FromDieType(type);
    public Material faceMaterial;
    public FaceRarity rarity;

    [Header("Values")]
    public int damage; // New independent damage value
    [Tooltip("For attack faces only: pending physical uses (final damage after Strength/modifiers) × this many hits. Ignored when type is not Damage.")]
    [Min(1)]
    public int damageAttackTimes = 1;
    public int armor;  // New independent armor value

    [Header("UI (card/picker/shop)")]
    public Sprite uiIcon;

    [Tooltip("Frame / panel art for this face in tooltips and face cards (e.g. UIRewardSlot when wired).")]
    public Sprite uiTooltipBackground;

    [Header("Game Actions")]
    [Tooltip("If true, all actions run when the die settles; if false, they run at turn end before damage resolves.")]
    public bool activateImmediately = true;
    [Tooltip("Executed in list order. Use + in the inspector to add multiple polymorphic actions.")]
    [SerializeReference] public List<IGameAction> actions = new List<IGameAction>();

    private string BuildDescription()
    {
        if (descriptionLines != null && descriptionLines.Count > 0)
        {
            var nonEmptyLines = new List<string>();
            for (var i = 0; i < descriptionLines.Count; i++)
            {
                var line = descriptionLines[i];
                if (!string.IsNullOrWhiteSpace(line))
                    nonEmptyLines.Add(line.Trim());
            }

            if (nonEmptyLines.Count > 0)
                return string.Join("\n\n", nonEmptyLines);
        }

        return string.IsNullOrEmpty(description) ? name : description;
    }
}