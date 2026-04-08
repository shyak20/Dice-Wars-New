using System;
using UnityEngine;

[Serializable]
public class ThornsAction : GameActionWithIcon
{
    [SerializeField] private int amount = 1;

    protected override ActionVisualId VisualKey => ActionVisualId.Thorns;

    public override void Execute(GameActionContext context)
    {
        context.CombatManager.AddThorns(amount);
        if (GameActionDebug.Enabled)
            Debug.Log("[ThornsAction] Thorns active — enemy takes 1 damage per attack this turn");
    }
}
