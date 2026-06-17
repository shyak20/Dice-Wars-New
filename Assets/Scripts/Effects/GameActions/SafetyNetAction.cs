using System;
using UnityEngine;

[Serializable]
public class SafetyNetAction : GameActionWithIcon
{
    protected override ActionVisualId VisualKey => ActionVisualId.SafetyNet;

    public override void Execute(GameActionContext context)
    {
        context.CombatManager.SetBustProtected();
        if (GameActionDebug.Enabled)
            Debug.Log("[SafetyNetAction] Bust protection active for this turn");
    }
}
