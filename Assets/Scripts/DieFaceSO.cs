using UnityEngine;

[CreateAssetMenu(fileName = "NewDieFace", menuName = "DiceGame/DieFace")]
public class DieFaceSO : ScriptableObject
{
    public int value = 1;
    public DieType type;
    public FaceEffect effect;
    public Material faceMaterial;
}