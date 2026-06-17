using System;
using UnityEngine;

[Serializable]
public class AddRollsAction : GameActionWithIcon
{
    [SerializeField] private int amount = 1;

    protected override ActionVisualId VisualKey => ActionVisualId.AddRolls;

    public override void Execute(GameActionContext context)
    {
        if (context.CombatManager == null) return;

        var delta = Mathf.Max(0, amount);
        context.CombatManager.AddRollsRemaining(delta);

        if (GameActionDebug.Enabled)
            Debug.Log($"[AddRollsAction] Added +{delta} roll(s) this turn");
    }
}
