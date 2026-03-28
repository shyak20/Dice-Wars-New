using UnityEngine;

[CreateAssetMenu(fileName = "NewFace", menuName = "DiceGame/DieFace")]
public class DieFaceSO : ScriptableObject
{
    public int value;
    public DieType type;
    public Material faceMaterial;
    public FaceRarity rarity;

    [Header("UI Visuals")]
    public Sprite faceIcon; // The image that will represent this face in the UI
}