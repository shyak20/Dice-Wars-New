using UnityEngine;

public enum FaceRarity { Common, Rare, Epic, Legendary }

[CreateAssetMenu(fileName = "NewFace", menuName = "DiceGame/DieFace")]
public class DieFaceSO : ScriptableObject
{
    public int value;
    public DieType type;
    public Material faceMaterial;
    public FaceRarity rarity;

    [Range(1, 100)]
    public int spawnWeight = 100; // Common = 100, Rare = 30, Legendary = 5, etc.
}