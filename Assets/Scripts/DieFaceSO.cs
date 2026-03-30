using UnityEngine;

[CreateAssetMenu(fileName = "NewFace", menuName = "DiceGame/DieFace")]
public class DieFaceSO : ScriptableObject
{
    [SerializeField] private string title;
    [SerializeField] private string description;

    public string Title => string.IsNullOrEmpty(title) ? name : title;
    public string Description => string.IsNullOrEmpty(description) ? name : description;
    public int value;
    public DieType type;
    public Material faceMaterial;
    public FaceRarity rarity;

    [Header("UI Visuals")]
    public Sprite faceIcon;

    [Header("Game Action")]
    [SerializeReference] public IGameAction action;
}