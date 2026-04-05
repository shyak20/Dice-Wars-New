using UnityEngine;

[CreateAssetMenu(fileName = "NewDie", menuName = "DiceGame/DieAsset")]
public class DieAssetSO : ScriptableObject
{
    public string dieName;
    public DieType dieType;

    [Header("Faces Configuration")]
    [Tooltip("The 6 Face SOs that define this die. Materials will be pulled from here.")]
    public DieFaceSO[] faces = new DieFaceSO[6];

    [Header("Combat — Max Power")]
    [Tooltip("How much max power increases when this die sits in the 3rd deck slot or later. The first two slots only use CombatManager base max power.")]
    [SerializeField] private int maxPowerContribution = 6;

    public int MaxPowerContribution => maxPowerContribution;
}