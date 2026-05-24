using System;
using UnityEngine;

/// <summary>
/// Marker: after the roll batch is gathered, if power is not at max (no Perfect Cast), reduce current power.
/// Handled in <see cref="CombatManager"/> (same timing as <see cref="AddPowerAction"/>).
/// </summary>
[Serializable]
public class ReducePowerUnlessPerfectCastAfterBatchAction : GameActionWithIcon
{
    [SerializeField, Min(1)] private int powerReduction = 2;

    public int PowerReduction => powerReduction;

    protected override ActionVisualId VisualKey => ActionVisualId.ReducePowerUnlessPerfectCast;

    public override void Execute(GameActionContext context)
    {
    }
}
