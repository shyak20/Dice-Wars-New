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
            ctx.Player.Heal(healAmount * ctx.CombatManager.GetAppliedMultiplier()));
    }
}
