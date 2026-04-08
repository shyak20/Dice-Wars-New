using System;
using UnityEngine;

[Serializable]
public class PrecisionAction : GameActionWithIcon
{
    [SerializeField] private int powerAmount = 1;

    protected override ActionVisualId VisualKey => ActionVisualId.Precision;

    public override void Execute(GameActionContext context)
    {
        context.CombatManager.QueuePrecisionChoice(powerAmount);
        if (GameActionDebug.Enabled)
            Debug.Log($"[PrecisionAction] Queued precision choice: +{powerAmount} power");
    }
}
