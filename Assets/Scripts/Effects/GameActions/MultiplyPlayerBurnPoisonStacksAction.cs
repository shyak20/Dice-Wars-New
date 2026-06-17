using System;
using UnityEngine;

[Serializable]
public class MultiplyPlayerBurnPoisonStacksAction : GameActionWithIcon
{
    [SerializeField, Min(2)] private int multiplier = 2;

    public int Multiplier => multiplier;

    // Reuse status-style icon slot; assign a dedicated icon later if needed.
    protected override ActionVisualId VisualKey => ActionVisualId.None;

    public override void Execute(GameActionContext context)
    {
        if (context?.Player == null)
            return;

        var statusCtx = new StatusEffectContext
        {
            CombatManager = context.CombatManager,
            Player = context.Player,
            Enemy = context.Enemy
        };

        context.Player.StatusEffects.MultiplyStacks<BurnEffectSO>(multiplier, statusCtx);
        context.Player.StatusEffects.MultiplyStacks<PoisonEffectSO>(multiplier, statusCtx);

        if (GameActionDebug.Enabled)
            Debug.Log($"[MultiplyPlayerBurnPoisonStacksAction] Multiplied player Burn + Poison stacks by x{multiplier}.");
    }
}
