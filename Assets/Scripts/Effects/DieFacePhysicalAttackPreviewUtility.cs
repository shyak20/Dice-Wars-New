using UnityEngine;

/// <summary>Live per-hit physical attack damage for die-face tooltip copy.</summary>
public static class DieFacePhysicalAttackPreviewUtility
{
    public static bool TryGetPerHitDamage(DieFaceSO face, DieFaceDescriptionContext context, out int perHitDamage)
    {
        perHitDamage = 0;
        if (face == null || face.type != DieType.Damage)
            return false;

        var combat = context?.Combat;
        if (combat != null)
            return combat.TryPreviewPhysicalAttackPerHit(face, out perHitDamage);

        if (face.damage > 0)
        {
            perHitDamage = face.damage;
            return true;
        }

        return false;
    }
}
