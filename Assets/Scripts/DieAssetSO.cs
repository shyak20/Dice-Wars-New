using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDie", menuName = "DiceGame/DieAsset")]
public class DieAssetSO : ScriptableObject
{
    public const int GemSocketCount = 2;

    public string dieName;
    public DieType dieType;

    /// <summary>Element line for this die; matches <see cref="DieFaceSO.Element"/> on attachable faces.</summary>
    public ElementType Element => ElementTypeExtensions.FromDieType(dieType);

    [Header("UI Visuals")]
    [Tooltip("Icon for this die in deck / reward / die-picker UI (not per-face art).")]
    public Sprite uiIcon;

    [Tooltip("Frame / panel art behind the full die tooltip (combat, shop, face picker).")]
    public Sprite uiTooltipBackground;

    [Header("Faces Configuration")]
    [Tooltip("The 6 Face SOs that define this die. Materials will be pulled from here.")]
    public DieFaceSO[] faces = new DieFaceSO[6];

    [Header("Gems (runtime deck instances)")]
    [Tooltip("Up to two permanent gems; null slots are empty. Filled via shop purchase flow only.")]
    [SerializeField] private GemSO[] socketedGems = new GemSO[GemSocketCount];

    [Header("Combat — Max Power")]
    [Tooltip("How much max power increases when this die sits in the 3rd deck slot or later. The first two slots only use CombatManager base max power.")]
    [SerializeField] private int maxPowerContribution = 6;

    public int MaxPowerContribution => maxPowerContribution;

    /// <summary>Non-null socketed gems in order (iteration only).</summary>
    public IEnumerable<GemSO> GetSocketedGems()
    {
        if (socketedGems == null)
            yield break;
        foreach (var g in socketedGems)
        {
            if (g != null)
                yield return g;
        }
    }

    /// <summary>Gem in a specific socket (0..GemSocketCount-1), or null when empty.</summary>
    public GemSO GetSocketedGemAt(int index)
    {
        EnsureGemSocketArray();
        if (index < 0 || index >= GemSocketCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Gem socket index must be 0..{GemSocketCount - 1}.");
        return socketedGems[index];
    }

    public int GetEmptyGemSocketCount()
    {
        EnsureGemSocketArray();
        var n = 0;
        for (var i = 0; i < GemSocketCount; i++)
        {
            if (socketedGems[i] == null)
                n++;
        }

        return n;
    }

    /// <summary>Permanent — first empty socket only.</summary>
    public bool TrySocketGem(GemSO gem)
    {
        if (gem == null)
            return false;
        EnsureGemSocketArray();
        for (var i = 0; i < GemSocketCount; i++)
        {
            if (socketedGems[i] != null)
                continue;
            socketedGems[i] = gem;
            return true;
        }

        return false;
    }

    /// <summary>Total charges where a matching roll contributes 0 power (sum of FreeFirstRollForThisDie params across socketed gems).</summary>
    public int SumGemNoPowerOnMatchCharges()
    {
        var total = 0;
        foreach (var g in GetSocketedGems())
        {
            if (g?.effects == null) continue;
            foreach (var e in g.effects)
            {
                if (e != null && e.kind == GemEffectKind.FreeFirstRollForThisDie)
                    total += Mathf.Max(0, e.param);
            }
        }
        return total;
    }

    private void EnsureGemSocketArray()
    {
        if (socketedGems == null || socketedGems.Length != GemSocketCount)
            socketedGems = new GemSO[GemSocketCount];
    }

    public bool CanAttachFace(DieFaceSO face)
    {
        if (face == null) return false;
        return face.MatchesDie(this);
    }

    /// <summary>Replaces one face and returns the previous face (for scrap / undo).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Index not 0–5.</exception>
    /// <exception cref="InvalidOperationException">New face element does not match this die.</exception>
    public DieFaceSO SwapFace(int index, DieFaceSO newFace)
    {
        if (faces == null || faces.Length < 6)
            throw new InvalidOperationException($"Die '{dieName}' must have at least 6 face slots.");
        if (faces.Length > 6)
            Debug.LogWarning(
                $"Die '{dieName}' has {faces.Length} face entries; gameplay uses only indices 0–5. Trim the array in the inspector when convenient.",
                this);

        if (index < 0 || index >= 6)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Face index must be 0–5.");
        if (newFace != null && !CanAttachFace(newFace))
            throw new InvalidOperationException($"Cannot attach face '{newFace.name}' to die '{dieName}' — element mismatch.");

        var old = faces[index];
        faces[index] = newFace;
        return old;
    }
}