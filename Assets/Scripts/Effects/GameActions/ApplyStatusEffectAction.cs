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
    /// Deferred faces: adds a pool row so the apply is visible until submit and can scale with Perfect Strike / bust.
    /// Immediate faces skip the pool — <see cref="DieFaceSO.activateImmediately"/> applies status when the die settles.
    /// </summary>
    public void AppendPoolContributionIfAny(FaceResult result, PlayerStatus player, bool activateImmediately)
    {
        if (activateImmediately) return;
        if (statusEffect == null || result == null || player == null) return;

        var applyStacks = stacks;
        if (statusEffect is BurnEffectSO && statusEffect.target == StatusEffectTarget.Enemy)
            applyStacks += player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        DieType poolType;
        int poolAmount;
        if (!statusEffect.TryGetRollFlyoutContribution(applyStacks, statusEffect.target, out poolType, out poolAmount))
        {
            poolType = statusEffect.ElementPoolDisplayRow;
            poolAmount = applyStacks;
        }

        if (poolAmount <= 0) return;

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolType = poolType,
            Amount = poolAmount,
            Icon = ResolveStatusIcon(),
            PoolSourceAction = this
        });
    }
}
