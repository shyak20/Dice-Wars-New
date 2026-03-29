using System;
using UnityEngine;

[Serializable]
public class CleanseAction : IGameAction
{
    public void Execute(GameActionContext context)
    {
        var ctx = new StatusEffectContext
        {
            CombatManager = context.CombatManager,
            Player = context.Player,
            Enemy = context.Enemy
        };

        context.Player.StatusEffects.ClearDebuffs(ctx);

        if (GameActionDebug.Enabled)
            Debug.Log("[Cleanse] Cleared all debuffs from player");
    }
}
