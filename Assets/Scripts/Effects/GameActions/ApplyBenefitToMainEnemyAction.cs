using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Enemy intent action for spawned adds: applies armor, heal, and/or a status effect to the Main Enemy (roster slot 0).
/// Configure one or more benefits; all configured parts resolve in a single intent step.
/// </summary>
[Serializable]
public class ApplyBenefitToMainEnemyAction : GameActionWithIcon
{
    [SerializeField, Min(0)] private int armorAmount;
    [SerializeField, Min(0)] private int healAmount;
    [SerializeField] private StatusEffectSO statusEffect;
    [SerializeField, Min(0)] private int statusStacks = 1;

    public int ArmorAmount => armorAmount;
    public int HealAmount => healAmount;
    public StatusEffectSO StatusEffectDefinition => statusEffect;
    public int StatusStacks => statusStacks;

    public bool HasConfiguredArmor => armorAmount > 0;
    public bool HasConfiguredHeal => healAmount > 0;
    public bool HasConfiguredStatus => statusEffect != null && statusStacks > 0;
    public bool IsStatusOnlyBenefit => HasConfiguredStatus && !HasConfiguredArmor && !HasConfiguredHeal;

    protected override ActionVisualId VisualKey => ActionVisualId.ApplyBenefitToMainEnemy;

    public Sprite ResolveIntentDisplayIcon()
    {
        if (IsStatusOnlyBenefit)
            return GameIconCatalog.GetStatusIcon(statusEffect);

        var actionIcon = ResolveActionIcon();
        if (actionIcon != null)
            return actionIcon;

        if (HasConfiguredArmor && !HasConfiguredHeal && !HasConfiguredStatus)
            return GameIconCatalog.GetElementIcon(DieType.Armor);

        if (HasConfiguredHeal && !HasConfiguredArmor && !HasConfiguredStatus)
            return GameIconCatalog.GetActionIcon(ActionVisualId.Heal);

        return null;
    }

    public string FormatIntentAmountLabel()
    {
        var parts = new StringBuilder();
        if (HasConfiguredArmor)
            AppendPart(parts, armorAmount);
        if (HasConfiguredHeal)
            AppendPart(parts, healAmount);
        if (HasConfiguredStatus)
            AppendPart(parts, statusStacks);
        return parts.ToString();
    }

    static void AppendPart(StringBuilder parts, int value)
    {
        if (parts.Length > 0)
            parts.Append('+');
        parts.Append(value);
    }

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null)
            return;

        if (!HasConfiguredArmor && !HasConfiguredHeal && !HasConfiguredStatus)
        {
            Debug.LogError("ApplyBenefitToMainEnemyAction: no benefit configured (armor, heal, or status).");
            return;
        }

        context.CombatManager.ApplyBenefitToMainEnemy(armorAmount, healAmount, statusEffect, statusStacks);

        if (GameActionDebug.Enabled)
        {
            var actingName = context.Enemy != null ? context.Enemy.name : "unknown";
            Debug.Log(
                $"[ApplyBenefitToMainEnemy] {actingName} supported Main Enemy — armor {armorAmount}, heal {healAmount}, status {(statusEffect != null ? statusEffect.effectName : "none")} x{statusStacks}.");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (armorAmount <= 0 && healAmount <= 0 && (statusEffect == null || statusStacks <= 0))
        {
            Debug.LogWarning(
                $"{nameof(ApplyBenefitToMainEnemyAction)}: configure at least one of {nameof(armorAmount)}, {nameof(healAmount)}, or {nameof(statusEffect)} + {nameof(statusStacks)}.");
        }

        if (statusEffect == null && statusStacks > 0)
        {
            Debug.LogWarning(
                $"{nameof(ApplyBenefitToMainEnemyAction)}: {nameof(statusStacks)} is set but {nameof(statusEffect)} is missing.");
        }

        if (statusEffect != null && statusStacks <= 0)
        {
            Debug.LogWarning(
                $"{nameof(ApplyBenefitToMainEnemyAction)}: assign {nameof(statusStacks)} when {nameof(statusEffect)} is set.");
        }
    }
#endif
}
