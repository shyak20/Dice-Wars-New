using System;
using UnityEngine;

[Serializable]
public class SafetyNetAction : IGameAction
{
    public void Execute(GameActionContext context)
    {
        context.CombatManager.SetBustProtected();
        if (GameActionDebug.Enabled)
            Debug.Log("[SafetyNetAction] Bust protection active for this turn");
    }
}
