using System;
using UnityEngine;

[Serializable]
public class ApplyStatusEffectAction : GameActionWithIcon
{
    [SerializeField] private StatusEffectSO statusEffect;
    [SerializeField] private int stacks = 1;

    public StatusEffectSO StatusEffectDefinition => statusEffect;

    public Sprite ResolveStatusIcon() =>
        GameIconCatalog.GetStatusIcon(statusEffect);

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

        var applyStacks = stacks;
        if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy && context.Player != null)
        {
            var pyro = context.Player.StatusEffects.GetStacks<PyromaniacEffectSO>();
            applyStacks += pyro;
        }

        manager.ApplyStatus(statusEffect, applyStacks, ctx);

        if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy && context.CombatManager != null)
            context.CombatManager.TurnRegistry?.RecordBurnApplied(applyStacks);

        if (GameActionDebug.Enabled)
            Debug.Log($"[ApplyStatusEffect] Applied {stacks} stacks of {statusEffect.effectName} to {statusEffect.target}");
    }

    /// <summary>Registers a separate pool/flyout row (e.g. Burn → Fire) so it is not merged into physical damage.</summary>
    public void AppendPoolContributionIfAny(FaceResult result, PlayerStatus player)
    {
        if (statusEffect == null || result == null || player == null) return;

        var applyStacks = stacks;
        if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy)
            applyStacks += player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        if (!statusEffect.TryGetRollFlyoutContribution(applyStacks, statusEffect.target, out var poolType, out var poolAmount))
            return;
        if (poolAmount <= 0) return;

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolType = poolType,
            Amount = poolAmount,
            Icon = ResolveStatusIcon()
        });
    }
}
