using System;
using UnityEngine;

[Serializable]
public class ThornsAction : IGameAction
{
    [SerializeField] private int amount = 1;

    public void Execute(GameActionContext context)
    {
        context.CombatManager.AddThorns(amount);
        if (GameActionDebug.Enabled)
            Debug.Log("[ThornsAction] Thorns active — enemy takes 1 damage per attack this turn");
    }
}
