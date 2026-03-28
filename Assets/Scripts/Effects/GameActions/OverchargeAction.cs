using System;
using UnityEngine;

[Serializable]
public class OverchargeAction : IGameAction
{
    [SerializeField] private int amount = 1;

    public void Execute(GameActionContext context)
    {
        context.CombatManager.AddOvercharge(amount);
    }
}
