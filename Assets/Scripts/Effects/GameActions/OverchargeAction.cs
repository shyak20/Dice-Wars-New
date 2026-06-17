using System;
using UnityEngine;

[Serializable]
public class OverchargeAction : GameActionWithIcon
{
    [SerializeField] private int amount = 1;

    public int OverchargeAmount => amount;

    protected override ActionVisualId VisualKey => ActionVisualId.Overcharge;

    public override void Execute(GameActionContext context)
    {
        context.CombatManager.AddOvercharge(amount);
        if (GameActionDebug.Enabled)
            Debug.Log($"[OverchargeAction] Added {amount} overcharge (total multiplier on Perfect Strike: {2 + amount})");
    }
}
