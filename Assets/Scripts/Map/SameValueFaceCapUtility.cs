using UnityEngine;

/// <summary>
/// Enforces per-die caps on how many faces may share the same <see cref="DieFaceSO.value"/>,
/// from the current act's <see cref="MapActDefinitionSO.maxSameNumericValueFacesPerDie"/>.
/// <see cref="DieType.Curse"/> faces are excluded from this count so curse slots stay replaceable when
/// their numeric <c>value</c> overlaps other faces (the cap still applies to non-curse faces only).
/// </summary>
public static class SameValueFaceCapUtility
{
    /// <summary>When there is no map act (legacy runs), no cap is applied.</summary>
    public static int GetMaxSameNumericValueFacesPerDie()
    {
        var act = RunManager.Instance != null ? RunManager.Instance.GetCurrentMapActDefinitionOrNull() : null;
        if (act == null)
            return int.MaxValue;
        return Mathf.Max(1, act.maxSameNumericValueFacesPerDie);
    }

    /// <summary>Only non-curse faces participate in the same-numeric-value cap.</summary>
    static bool FaceCountsTowardSameValueCap(DieFaceSO face, int targetValue) =>
        face != null && face.type != DieType.Curse && face.value == targetValue;

    public static bool CanReplaceFaceWithoutViolatingCap(DieAssetSO die, int slotIndex, DieFaceSO newFace)
    {
        if (die == null || newFace == null)
            return false;
        if (die.faces == null || slotIndex < 0 || slotIndex >= die.faces.Length)
            return false;

        var max = GetMaxSameNumericValueFacesPerDie();
        if (max >= int.MaxValue)
            return true;

        var targetValue = newFace.value;
        var countAfter = 0;
        for (var i = 0; i < die.faces.Length; i++)
        {
            var f = i == slotIndex ? newFace : die.faces[i];
            if (FaceCountsTowardSameValueCap(f, targetValue))
                countAfter++;
        }

        return countAfter <= max;
    }

    public static bool DieHasAnyLegalReplacementSlot(DieAssetSO die, DieFaceSO newFace)
    {
        if (die == null || newFace == null || !die.CanAttachFace(newFace))
            return false;
        if (GetMaxSameNumericValueFacesPerDie() >= int.MaxValue)
            return true;
        for (var i = 0; i < die.faces.Length; i++)
        {
            if (CanReplaceFaceWithoutViolatingCap(die, i, newFace))
                return true;
        }

        return false;
    }
}
