using System;
using UnityEngine;

[Serializable]
public class HealAction : IGameAction
{
    [SerializeField] private int amount = 1;

    public void Execute(GameActionContext context)
    {
        var healAmount = amount;
        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var finalHeal = healAmount * ctx.CombatManager.GetAppliedMultiplier();
            if (GameActionDebug.Enabled)
                Debug.Log($"[HealAction] Healing {finalHeal} HP (base: {healAmount}, multiplier: {ctx.CombatManager.GetAppliedMultiplier()})");
            ctx.Player.Heal(finalHeal);
        });
    }
}
