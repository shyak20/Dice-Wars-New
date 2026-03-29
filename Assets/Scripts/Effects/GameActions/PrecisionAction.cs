using System;
using UnityEngine;

[Serializable]
public class PrecisionAction : IGameAction
{
    [SerializeField] private int powerAmount = 1;

    public void Execute(GameActionContext context)
    {
        context.CombatManager.QueuePrecisionChoice(powerAmount);
        if (GameActionDebug.Enabled)
            Debug.Log($"[PrecisionAction] Queued precision choice: +{powerAmount} power");
    }
}
