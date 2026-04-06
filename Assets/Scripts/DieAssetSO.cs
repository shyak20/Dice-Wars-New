using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDie", menuName = "DiceGame/DieAsset")]
public class DieAssetSO : ScriptableObject
{
    public string dieName;
    public DieType dieType;

    /// <summary>Element line for this die; matches <see cref="DieFaceSO.Element"/> on attachable faces.</summary>
    public ElementType Element => ElementTypeExtensions.FromDieType(dieType);

    [Header("UI Visuals")]
    [Tooltip("Icon for this die in deck / reward / die-picker UI (not per-face art).")]
    public Sprite uiIcon;

    [Header("Faces Configuration")]
    [Tooltip("The 6 Face SOs that define this die. Materials will be pulled from here.")]
    public DieFaceSO[] faces = new DieFaceSO[6];

    [Header("Combat — Max Power")]
    [Tooltip("How much max power increases when this die sits in the 3rd deck slot or later. The first two slots only use CombatManager base max power.")]
    [SerializeField] private int maxPowerContribution = 6;

    public int MaxPowerContribution => maxPowerContribution;

    public bool CanAttachFace(DieFaceSO face)
    {
        if (face == null) return false;
        return ElementTypeExtensions.FromDieType(dieType) == ElementTypeExtensions.FromDieType(face.type);
    }

    /// <summary>Replaces one face and returns the previous face (for scrap / undo).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Index not 0–5.</exception>
    /// <exception cref="InvalidOperationException">New face element does not match this die.</exception>
    public DieFaceSO SwapFace(int index, DieFaceSO newFace)
    {
        if (faces == null || faces.Length != 6)
            throw new InvalidOperationException($"Die '{dieName}' must have exactly 6 face slots.");
        if (index < 0 || index >= 6)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Face index must be 0–5.");
        if (newFace != null && !CanAttachFace(newFace))
            throw new InvalidOperationException($"Cannot attach face '{newFace.name}' to die '{dieName}' — element mismatch.");

        var old = faces[index];
        faces[index] = newFace;
        return old;
    }
}