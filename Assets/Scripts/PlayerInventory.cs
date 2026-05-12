using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Read-only helpers over <see cref="PlayerDataSO.currentDeck"/> for reward / picker flows.
/// </summary>
public static class PlayerInventory
{
    public static List<DieAssetSO> GetDiceMatchingElement(PlayerDataSO data, ElementType element)
    {
        if (data?.currentDeck == null) return new List<DieAssetSO>();
        return data.currentDeck
            .Where(d => d != null && ElementTypeExtensions.FromDieType(d.dieType) == element)
            .ToList();
    }

    public static List<DieAssetSO> GetDiceMatchingFace(PlayerDataSO data, DieFaceSO face)
    {
        if (face == null || data == null) return new List<DieAssetSO>();
        if (face.type == DieType.Curse)
        {
            if (data.currentDeck == null) return new List<DieAssetSO>();
            return data.currentDeck.Where(d => d != null).ToList();
        }

        return GetDiceMatchingElement(data, ElementTypeExtensions.FromDieType(face.type));
    }

    /// <summary>
    /// Dice that match the face's element and have at least one slot where equipping this face would not exceed
    /// the act's <see cref="MapActDefinitionSO.maxSameNumericValueFacesPerDie"/>.
    /// </summary>
    public static List<DieAssetSO> GetDiceEligibleForFaceReplacement(PlayerDataSO data, DieFaceSO face)
    {
        if (face == null || data?.currentDeck == null) return new List<DieAssetSO>();
        var list = new List<DieAssetSO>();
        foreach (var d in data.currentDeck)
        {
            if (d != null && SameValueFaceCapUtility.DieHasAnyLegalReplacementSlot(d, face))
                list.Add(d);
        }

        return list;
    }

    public static bool HasDieSupportingFace(PlayerDataSO data, DieFaceSO face) =>
        face != null && data != null && GetDiceEligibleForFaceReplacement(data, face).Count > 0;

    /// <summary>Deck dice that can still receive a permanent gem (at least one empty socket).</summary>
    public static List<DieAssetSO> GetDiceWithEmptyGemSocket(PlayerDataSO data)
    {
        if (data?.currentDeck == null) return new List<DieAssetSO>();
        return data.currentDeck.Where(d => d != null && d.GetEmptyGemSocketCount() > 0).ToList();
    }

    public static bool HasDieWithEmptyGemSocket(PlayerDataSO data) =>
        data != null && GetDiceWithEmptyGemSocket(data).Count > 0;
}
