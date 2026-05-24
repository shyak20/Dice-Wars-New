using System;
using UnityEngine;

/// <summary>Immediately raises max power for the rest of this combat.</summary>
[Serializable]
public class IncreaseCombatMaxPowerAction : GameActionWithIcon
{
    [SerializeField, Min(1)] private int amount = 1;

    public int Amount => amount;

    protected override ActionVisualId VisualKey => ActionVisualId.IncreaseCombatMaxPower;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || amount <= 0)
            return;

        context.CombatManager.AddCombatMaxPowerBonus(amount);

        if (GameActionDebug.Enabled)
            Debug.Log($"[IncreaseCombatMaxPower] +{amount} max power this combat.");
    }
}
