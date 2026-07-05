using UnityEngine;

/// <summary>
/// Resolves the first index-backed effect icon to show on a 3D die face indicator.
/// Skips poison/burn statuses, burn-themed action icons, and base damage/defense element art.
/// </summary>
public static class DieFaceEffectIconResolver
{
    static readonly int[] TopologyFaceLabels = { 1, 6, 2, 5, 3, 4 };

    public static int TopologyIndexToFaceLabel(int topologyIndex) =>
        topologyIndex >= 0 && topologyIndex < TopologyFaceLabels.Length
            ? TopologyFaceLabels[topologyIndex]
            : throw new System.ArgumentOutOfRangeException(nameof(topologyIndex));

    public static bool TryResolve(DieFaceSO face, out Sprite icon)
    {
        icon = null;
        if (face?.actions == null || face.actions.Count == 0)
            return false;

        for (var i = 0; i < face.actions.Count; i++)
        {
            var sprite = TryResolveFromAction(face.actions[i]);
            if (sprite == null)
                continue;

            icon = sprite;
            return true;
        }

        return false;
    }

    static Sprite TryResolveFromAction(IGameAction action)
    {
        if (action is ApplyStatusEffectAction apply)
        {
            var def = apply.StatusEffectDefinition;
            if (def == null || IsExcludedStatus(def))
                return null;

            return GameIconCatalog.GetStatusIcon(def);
        }

        if (action is ApplyBenefitToMainEnemyAction benefit)
        {
            if (benefit.IsStatusOnlyBenefit)
            {
                var def = benefit.StatusEffectDefinition;
                if (def != null && !IsExcludedStatus(def))
                    return GameIconCatalog.GetStatusIcon(def);
                return null;
            }

            var id = benefit.GetActionVisualId();
            if (id != ActionVisualId.None && !IsExcludedBurnAction(id))
                return GameIconCatalog.GetActionIcon(id);

            if (benefit.HasConfiguredHeal && !benefit.HasConfiguredArmor && !benefit.HasConfiguredStatus)
                return GameIconCatalog.GetActionIcon(ActionVisualId.Heal);

            return null;
        }

        if (action is GameActionWithIcon withIcon)
        {
            var id = withIcon.GetActionVisualId();
            if (id == ActionVisualId.None || IsExcludedBurnAction(id))
                return null;

            return GameIconCatalog.GetActionIcon(id);
        }

        if (action is FaceResolveModifierWithIcon modWithIcon)
        {
            var id = modWithIcon.GetActionVisualId();
            if (id == ActionVisualId.None || IsExcludedBurnAction(id))
                return null;

            return GameIconCatalog.GetActionIcon(id);
        }

        return null;
    }

    static bool IsExcludedStatus(StatusEffectSO definition) =>
        definition is BurnEffectSO or PoisonEffectSO;

    static bool IsExcludedBurnAction(ActionVisualId id) =>
        id is ActionVisualId.DamageFromEnemyBurnStacks
            or ActionVisualId.InstantBurnProcFromStacks
            or ActionVisualId.ConsumeBurnForMaxHp
            or ActionVisualId.ArmorFromEnemyBurnStacks
            or ActionVisualId.BonusArmorBurnWhenStruck
            or ActionVisualId.BonusDamageVsBurnThreshold
            or ActionVisualId.PrimeNextFireRollDoubleEnemyBurn;
}
