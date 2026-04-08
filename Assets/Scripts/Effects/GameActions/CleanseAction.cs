using System;
using UnityEngine;

[Serializable]
public class CleanseAction : GameActionWithIcon
{
    [SerializeField] private int stacks = 1;

    protected override ActionVisualId VisualKey => ActionVisualId.Cleanse;

    public override void Execute(GameActionContext context)
    {
        var cleanseStacks = stacks;
        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var finalStacks = cleanseStacks * ctx.CombatManager.GetAppliedMultiplier();
            var statusCtx = new StatusEffectContext
            {
                CombatManager = ctx.CombatManager,
                Player = ctx.Player,
                Enemy = ctx.Enemy
            };

            var reduced = ctx.Player.StatusEffects.ReduceRandomDebuffStacks(finalStacks, statusCtx);

            if (GameActionDebug.Enabled)
                Debug.Log(reduced
                    ? $"[Cleanse] Reduced a random debuff by {finalStacks} stack(s) (base: {cleanseStacks}, multiplier: {ctx.CombatManager.GetAppliedMultiplier()})"
                    : "[Cleanse] No debuffs to cleanse");
        });
    }
}
