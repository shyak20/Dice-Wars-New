using UnityEngine;

[CreateAssetMenu(fileName = "NewFace", menuName = "DiceGame/DieFace")]
public class DieFaceSO : ScriptableObject
{
    public string Title;
    public string Description;
    public int value;
    public DieType type;
    public Material faceMaterial;
    public FaceRarity rarity;

    [Header("UI Visuals")]
    public Sprite faceIcon;

    [Header("Game Action")]
    [SerializeReference] public IGameAction action;
}