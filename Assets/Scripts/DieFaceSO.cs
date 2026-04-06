using UnityEngine;

[CreateAssetMenu(fileName = "NewFace", menuName = "DiceGame/DieFace")]
public class DieFaceSO : ScriptableObject
{
    [SerializeField] private string title;
    [SerializeField] private string description;

    public string Title => string.IsNullOrEmpty(title) ? name : title;
    public string Description => string.IsNullOrEmpty(description) ? name : description;

    public int value; // Keeping this for the Power Bar calculation
    public DieType type;

    /// <summary>Socketing element (maps from <see cref="type"/>; Physical = Damage, Defense = Armor).</summary>
    public ElementType Element => ElementTypeExtensions.FromDieType(type);
    public Material faceMaterial;
    public FaceRarity rarity;

    [Header("Values")]
    public int damage; // New independent damage value
    public int armor;  // New independent armor value

    [Header("UI Visuals")]
    public Sprite faceIcon;

    [Header("Game Action")]
    public bool activateImmediately = true; // Toggle for deferred execution
    [SerializeReference] public IGameAction action;
}