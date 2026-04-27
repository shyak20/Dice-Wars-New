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

    PoolRowKey ResolvePoolRowKey()
    {
        if (statusEffect != null)
            return PoolRowKey.Custom(statusEffect.name);
        return PoolRowKey.Custom("Status");
    }

    public override void Execute(GameActionContext context)
    {
        if (statusEffect == null)
        {
            Debug.LogError("ApplyStatusEffectAction: statusEffect is not assigned!");
            return;
        }

        var manager = statusEffect.target == StatusEffectTarget.Player
            ? context.Player.StatusEffects
            : context.Enemy.StatusEffects;

        var ctx = new StatusEffectContext
        {
            CombatManager = context.CombatManager,
            Player = context.Player,
            Enemy = context.Enemy
        };

        int applyStacks;
        if (context.PendingApplyStackOverrides != null)
        {
            if (!context.PendingApplyStackOverrides.TryGetValue(this, out applyStacks))
                applyStacks = 0;
        }
        else
        {
            applyStacks = stacks;
            if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy && context.Player != null)
            {
                var pyro = context.Player.StatusEffects.GetStacks<PyromaniacEffectSO>();
                applyStacks += pyro;
            }
        }

        if (applyStacks <= 0) return;

        manager.ApplyStatus(statusEffect, applyStacks, ctx);

        if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy && context.CombatManager != null)
            context.CombatManager.TurnRegistry?.RecordBurnApplied(applyStacks);

        if (GameActionDebug.Enabled)
            Debug.Log($"[ApplyStatusEffect] Applied {applyStacks} stacks of {statusEffect.effectName} to {statusEffect.target}");
    }

    /// <summary>
    /// Deferred faces: pool row until submit (scales with Perfect Strike / bust).
    /// Immediate faces: adds a flyout-only row (not in pending pool snapshot) so the status icon can arc to the element container.
    /// </summary>
    public void AppendPoolContributionIfAny(FaceResult result, PlayerStatus player, bool activateImmediately)
    {
        if (statusEffect == null || result == null || player == null) return;

        var applyStacks = stacks;
        if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy)
            applyStacks += player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        if (applyStacks <= 0) return;

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = ResolvePoolRowKey(),
            Amount = applyStacks,
            Icon = ResolveStatusIcon(),
            PoolSourceAction = activateImmediately ? null : this,
            VisualFlyoutOnly = activateImmediately
        });
    }
}
