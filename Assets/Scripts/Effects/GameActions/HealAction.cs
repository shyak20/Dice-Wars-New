using System;
using UnityEngine;

[Serializable]
public class HealAction : IGameAction
{
    [SerializeField] private int amount = 1;

    public void Execute(GameActionContext context)
    {
        var player = context.Player;
        var healAmount = amount;
        context.CombatManager.QueueTurnEndAction(() => player.Heal(healAmount));
    }
}
