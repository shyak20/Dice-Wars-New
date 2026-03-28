using System;
using UnityEngine;

[Serializable]
public class EchoAction : IGameAction
{
    public void Execute(GameActionContext context)
    {
        var refund = context.TriggeringFace.Face.value;
        context.CombatManager.RefundPower(refund);
        if (GameActionDebug.Enabled)
            Debug.Log($"[EchoAction] Refunded {refund} power cost");
    }
}
