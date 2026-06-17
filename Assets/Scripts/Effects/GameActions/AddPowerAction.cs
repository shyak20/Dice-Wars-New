using System;
using UnityEngine;

/// <summary>
/// Optional bonus power: after all dice in the roll batch are gathered into combat state, the player is prompted to add +X to the power bar (same UI as <see cref="PrecisionAction"/>).
/// <see cref="CombatManager"/> enqueues the choice after the batch gather loop, not during per-face immediate action execution.
/// </summary>
[Serializable]
public class AddPowerAction : GameActionWithIcon
{
    [SerializeField] private int powerAmount = 1;

    public int PowerAmount => powerAmount;

    protected override ActionVisualId VisualKey => ActionVisualId.AddPower;

    public override void Execute(GameActionContext context)
    {
        // Intentionally empty: precision choices are queued in CombatManager after batch gather (see CoRollBatchPipeline).
    }
}
