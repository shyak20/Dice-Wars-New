using System;
using UnityEngine;

/// <summary>
/// Marker: after the roll batch is gathered, if this roll did not Perfect Cast and did not Bust,
/// raise max cast power for the rest of this combat. Handled in <see cref="CombatManager"/>
/// (same timing as <see cref="ReducePowerUnlessPerfectCastAfterBatchAction"/>).
/// </summary>
[Serializable]
public class IncreaseCombatMaxPowerAction : GameActionWithIcon
{
    [SerializeField, Min(1)] private int amount = 1;

    public int Amount => amount;

    protected override ActionVisualId VisualKey => ActionVisualId.IncreaseCombatMaxPower;

    public override void Execute(GameActionContext context)
    {
        // Applied in CombatManager.ApplyPostBatchFaceEffects after perfect/bust eligibility is known.
    }
}
