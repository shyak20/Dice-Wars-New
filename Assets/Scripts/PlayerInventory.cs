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
        return GetDiceMatchingElement(data, ElementTypeExtensions.FromDieType(face.type));
    }

    public static bool HasDieSupportingFace(PlayerDataSO data, DieFaceSO face) =>
        face != null && data != null && GetDiceMatchingFace(data, face).Count > 0;

    /// <summary>Deck dice that can still receive a permanent gem (at least one empty socket).</summary>
    public static List<DieAssetSO> GetDiceWithEmptyGemSocket(PlayerDataSO data)
    {
        if (data?.currentDeck == null) return new List<DieAssetSO>();
        return data.currentDeck.Where(d => d != null && d.GetEmptyGemSocketCount() > 0).ToList();
    }

    public static bool HasDieWithEmptyGemSocket(PlayerDataSO data) =>
        data != null && GetDiceWithEmptyGemSocket(data).Count > 0;
}
