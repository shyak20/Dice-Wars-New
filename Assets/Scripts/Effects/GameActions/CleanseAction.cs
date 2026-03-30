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

        var removed = context.Player.StatusEffects.RemoveRandomDebuff(ctx);

        if (GameActionDebug.Enabled)
            Debug.Log(removed
                ? "[Cleanse] Removed a random debuff from player"
                : "[Cleanse] No debuffs to cleanse");
    }
}
