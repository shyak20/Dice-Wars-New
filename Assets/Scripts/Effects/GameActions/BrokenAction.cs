using System;
using UnityEngine;

[Serializable]
public class BrokenAction : GameActionWithIcon
{
    protected override ActionVisualId VisualKey => ActionVisualId.Broken;

    public override void Execute(GameActionContext context)
    {
        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var armorRemoved = ctx.Enemy.GetCurrentArmor();
            if (armorRemoved > 0)
            {
                ctx.Enemy.ResetArmor();
                if (GameActionDebug.Enabled)
                    Debug.Log($"[Broken] Removed {armorRemoved} armor from enemy");
            }
            else if (GameActionDebug.Enabled)
            {
                Debug.Log("[Broken] Enemy had no armor to remove");
            }
        });

        if (GameActionDebug.Enabled)
            Debug.Log("[Broken] Queued armor strip for end of turn");
    }
}
