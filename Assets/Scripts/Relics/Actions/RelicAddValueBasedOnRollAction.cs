using System;
using UnityEngine;

/// <summary>
/// Relic hook mirroring <see cref="AddValueBasedOnRollAction"/>. <see cref="AddValueBasedOnRollDuration.SameRoll"/> uses relic modify / after-power phases;
/// <see cref="AddValueBasedOnRollDuration.SameTurn"/> and <see cref="AddValueBasedOnRollDuration.EntireCombat"/> register watchers at <see cref="RelicPhases.CombatStart"/>.
/// </summary>
[Serializable]
public sealed class RelicAddValueBasedOnRollAction : RelicGameActionBase
{
    [Tooltip("Face value (after status modifiers) must equal this for the bonus to apply.")]
    public int requiredFaceValue = 6;

    public RollBonusType bonusType = RollBonusType.Armor;

    [Tooltip("Armor or damage added to the matching roll, or burn stacks when Bonus Type is Burn.")]
    public int amount = 1;

    [Tooltip("Required when Bonus Type is Burn.")]
    public BurnEffectSO burnDefinition;

    public AddValueBasedOnRollDuration duration = AddValueBasedOnRollDuration.SameRoll;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx == null) return;

        if (ctx.RelicPhase == RelicPhases.CombatStart)
        {
            if (duration == AddValueBasedOnRollDuration.SameRoll) return;
            if (ctx.CombatManager == null) return;
            ctx.CombatManager.RegisterValueBasedRollWatcher(requiredFaceValue, bonusType, amount, burnDefinition, duration, fromRelicCombatStart: true);
            return;
        }

        if (ctx.RelicPhase == RelicPhases.AfterEnemyTurnPlayerTurnStart)
        {
            if (duration != AddValueBasedOnRollDuration.SameTurn) return;
            if (ctx.CombatManager == null) return;
            ctx.CombatManager.RegisterValueBasedRollWatcher(requiredFaceValue, bonusType, amount, burnDefinition, duration, fromRelicCombatStart: false);
            return;
        }

        if (duration != AddValueBasedOnRollDuration.SameRoll) return;

        AddValueBasedOnRollAction.ExecuteSameRollBonus(ctx, requiredFaceValue, bonusType, amount, burnDefinition);
    }
}
