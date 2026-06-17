using System;
using UnityEngine;

[Serializable]
public class ReducePlayerMaxPowerAction : GameActionWithIcon
{
    [SerializeField, Min(1)] private int reductionAmount = 1;
    [SerializeField, Min(1)] private int minimumAllowedMaxPower = 1;

    public int ReductionAmount => reductionAmount;
    public int MinimumAllowedMaxPower => minimumAllowedMaxPower;

    protected override ActionVisualId VisualKey => ActionVisualId.None;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null)
            return;

        // Enemy-intent use case: combat-only debuff, reset when combat ends.
        context.CombatManager.ApplyEnemyMaxPowerReductionForCombat(reductionAmount, minimumAllowedMaxPower);

        if (GameActionDebug.Enabled)
            Debug.Log($"[ReducePlayerMaxPowerAction] Reduced max power by {reductionAmount} (min floor: {minimumAllowedMaxPower}).");
    }
}
