using System;
using UnityEngine;

[Serializable]
public class ImmuneAction : IGameAction
{
    public void Execute(GameActionContext context)
    {
        context.CombatManager.SetImmune();
        if (GameActionDebug.Enabled)
            Debug.Log("[ImmuneAction] Immune active — enemy attacks capped to 1 damage this turn");
    }
}
