using System;
using UnityEngine;

[Serializable]
public class CleanseAction : GameActionWithIcon
{
    [SerializeField] private int stacks = 1;

    public int CleanseStacks => stacks;

    protected override ActionVisualId VisualKey => ActionVisualId.Cleanse;

    public override void Execute(GameActionContext context)
    {
        if (context == null || context.CombatManager == null)
            return;

        var cleanseStacks = stacks;
        var fromEnemyAction = context != null && context.SourceEnemyAction != null;

        if (fromEnemyAction)
        {
            var finalStacks = cleanseStacks;
            var statusCtx = new StatusEffectContext
            {
                CombatManager = context.CombatManager,
                Player = context.Player,
                Enemy = context.Enemy
            };

            var targetStatusManager = context.Enemy?.StatusEffects;
            var ownerTarget = StatusEffectTarget.Enemy;
            var reduced = targetStatusManager != null &&
                          targetStatusManager.ReduceRandomDebuffStacksForTarget(finalStacks, statusCtx, ownerTarget);

            if (GameActionDebug.Enabled)
                Debug.Log(reduced
                    ? $"[Cleanse] Enemy intent reduced a random debuff by {finalStacks} stack(s)."
                    : "[Cleanse] No debuffs to cleanse");
            return;
        }

        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var finalStacks = cleanseStacks * ctx.CombatManager.GetAppliedMultiplier();
            var statusCtx = new StatusEffectContext
            {
                CombatManager = ctx.CombatManager,
                Player = ctx.Player,
                Enemy = ctx.Enemy
            };

            var targetStatusManager = ctx.Player?.StatusEffects;
            var reduced = targetStatusManager != null &&
                          targetStatusManager.ReduceRandomDebuffStacksForTarget(finalStacks, statusCtx, StatusEffectTarget.Player);

            if (GameActionDebug.Enabled)
                Debug.Log(reduced
                    ? $"[Cleanse] Reduced a random debuff by {finalStacks} stack(s) (base: {cleanseStacks}, multiplier: {ctx.CombatManager.GetAppliedMultiplier()})"
                    : "[Cleanse] No debuffs to cleanse");
        });
    }
}
