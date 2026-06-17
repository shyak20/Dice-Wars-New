using System;
using UnityEngine;

[Serializable]
public class ApplyStatusEffectAction : GameActionWithIcon
{
    [SerializeField] private StatusEffectSO statusEffect;
    [SerializeField] private int stacks = 1;

    public int ConfiguredStacks => stacks;

    public StatusEffectSO StatusEffectDefinition => statusEffect;

    public Sprite ResolveStatusIcon() =>
        GameIconCatalog.GetStatusIcon(statusEffect);

    static PoolRowKey ResolvePoolRowKey(StatusEffectSO effect) =>
        effect != null ? PoolRowKey.Custom(effect.name) : PoolRowKey.Custom("Status");

    /// <summary>Final stack count after Pyromaniac, fire-double, etc. Shared by die faces and gems.</summary>
    public static int ResolveApplyStacks(StatusEffectSO statusEffect, int baseStacks, GameActionContext context, FaceResult face)
    {
        if (statusEffect == null || baseStacks <= 0)
            return 0;

        var applyStacks = baseStacks;
        if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy && context?.Player != null)
            applyStacks += context.Player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        if (face != null)
            applyStacks = face.ApplyFireDoubleToEnemyBurnStacks(applyStacks, statusEffect);

        return applyStacks;
    }

    public static void ApplyFromContext(GameActionContext context, StatusEffectSO statusEffect, int applyStacks)
    {
        if (statusEffect == null || applyStacks <= 0 || context == null)
            return;

        var statusCtx = new StatusEffectContext
        {
            CombatManager = context.CombatManager,
            Player = context.Player,
            Enemy = context.Enemy
        };

        if (statusEffect.target == StatusEffectTarget.Player)
        {
            if (context.Player == null)
                return;
            context.Player.StatusEffects.ApplyStatus(statusEffect, applyStacks, statusCtx);
        }
        else
        {
            if (context.Enemy == null)
                return;
            context.Enemy.StatusEffects.ApplyStatus(statusEffect, applyStacks, statusCtx);
        }

        if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy && context.CombatManager != null)
            context.CombatManager.TurnRegistry?.RecordBurnApplied(applyStacks);

        if (GameActionDebug.Enabled)
            Debug.Log($"[ApplyStatusEffect] Applied {applyStacks} stacks of {statusEffect.effectName} to {statusEffect.target}");
    }

    /// <summary>
    /// Deferred faces: pool row until submit (scales with Perfect Strike / bust).
    /// Immediate faces / gems: flyout-only row so the status icon can arc to the element container.
    /// </summary>
    public static void AppendPoolContribution(
        FaceResult result,
        PlayerStatus player,
        StatusEffectSO statusEffect,
        int baseStacks,
        bool visualFlyoutOnly,
        ApplyStatusEffectAction poolSourceAction = null)
    {
        if (statusEffect == null || result == null || player == null || baseStacks <= 0)
            return;

        var previewCtx = new GameActionContext { Player = player };
        var applyStacks = ResolveApplyStacks(statusEffect, baseStacks, previewCtx, result);
        if (applyStacks <= 0)
            return;

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = ResolvePoolRowKey(statusEffect),
            Amount = applyStacks,
            Icon = GameIconCatalog.GetStatusIcon(statusEffect),
            PoolSourceAction = visualFlyoutOnly ? null : poolSourceAction,
            VisualFlyoutOnly = visualFlyoutOnly
        });
    }

    public override void Execute(GameActionContext context)
    {
        if (statusEffect == null)
        {
            Debug.LogError("ApplyStatusEffectAction: statusEffect is not assigned!");
            return;
        }

        int applyStacks;
        if (context.PendingApplyStackOverrides != null)
        {
            if (!context.PendingApplyStackOverrides.TryGetValue(this, out applyStacks))
                applyStacks = 0;
        }
        else
            applyStacks = ResolveApplyStacks(statusEffect, stacks, context, context.TriggeringFace);

        ApplyFromContext(context, statusEffect, applyStacks);
    }

    public void AppendPoolContributionIfAny(FaceResult result, PlayerStatus player, bool activateImmediately)
    {
        if (statusEffect == null || result == null || player == null)
            return;

        AppendPoolContribution(
            result,
            player,
            statusEffect,
            stacks,
            activateImmediately,
            activateImmediately ? null : this);
    }
}
