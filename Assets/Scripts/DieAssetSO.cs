using UnityEngine;

[CreateAssetMenu(fileName = "NewDieAsset", menuName = "DiceGame/DieAsset")]
public class DieAssetSO : ScriptableObject
{
    public string dieName;
    public DieType dieType;

    // We remove OnValidate entirely to prevent the TypeTree crash.
    // Instead, we just keep the array.
    public DieFaceSO[] faces = new DieFaceSO[6];
}