using UnityEngine;

[CreateAssetMenu(fileName = "NewDie", menuName = "DiceGame/DieAsset")]
public class DieAssetSO : ScriptableObject
{
    public string dieName;
    public DieType dieType;

    [Header("Faces Configuration")]
    [Tooltip("The 6 Face SOs that define this die. Materials will be pulled from here.")]
    public DieFaceSO[] faces = new DieFaceSO[6];
}