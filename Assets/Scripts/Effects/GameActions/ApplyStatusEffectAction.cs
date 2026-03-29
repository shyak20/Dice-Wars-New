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

        manager.ApplyStatus(statusEffect, stacks, ctx);

        if (GameActionDebug.Enabled)
            Debug.Log($"[ApplyStatusEffect] Applied {stacks} stacks of {statusEffect.effectName} to {statusEffect.target}");
    }
}
