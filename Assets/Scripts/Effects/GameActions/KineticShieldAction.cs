using System;
using UnityEngine;

[Serializable]
public class KineticShieldAction : IGameAction
{
    public void Execute(GameActionContext context)
    {
        context.CombatManager.ActivateKineticShield();

        if (GameActionDebug.Enabled)
            Debug.Log("[KineticShield] Activated — +1 armor for each subsequent die this turn");
    }
}
