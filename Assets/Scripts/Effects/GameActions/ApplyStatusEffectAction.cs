using System;
using UnityEngine;

[Serializable]
public class ApplyStatusEffectAction : IGameAction
{
    [SerializeField] private StatusEffectSO statusEffect;
    [SerializeField] private int stacks = 1;

    public void Execute(GameActionContext context)
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
}
