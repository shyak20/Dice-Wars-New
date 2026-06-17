using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Relic hook mirroring <see cref="AddValueBasedOnRollAction"/>. <see cref="AddValueBasedOnRollDuration.SameRoll"/> uses relic modify / after-power phases;
/// <see cref="AddValueBasedOnRollDuration.SameTurn"/> and <see cref="AddValueBasedOnRollDuration.EntireCombat"/> register watchers at <see cref="RelicPhases.CombatStart"/>.
/// </summary>
[Serializable]
public sealed class RelicAddValueBasedOnRollAction : RelicGameActionBase, ISerializationCallbackReceiver
{
    [SerializeField, HideInInspector, FormerlySerializedAs("requiredFaceValues")]
    int[] legacyRequiredFaceValues;

    public FaceValueMatchSet requiredFaceValues = new FaceValueMatchSet();

    public RollBonusType bonusType = RollBonusType.Armor;

    [Tooltip("Armor or damage added to the matching roll, or burn stacks when Bonus Type is Burn.")]
    public int amount = 1;

    [Tooltip("Required when Bonus Type is Burn.")]
    public BurnEffectSO burnDefinition;

    public AddValueBasedOnRollDuration duration = AddValueBasedOnRollDuration.SameRoll;

    public void OnBeforeSerialize() => MigrateLegacy();

    public void OnAfterDeserialize() => MigrateLegacy();

    void MigrateLegacy()
    {
        requiredFaceValues ??= new FaceValueMatchSet();
        requiredFaceValues.MigrateLegacyIntArray(legacyRequiredFaceValues);
        legacyRequiredFaceValues = null;
    }

    public override void Execute(GameActionContext ctx)
    {
        if (ctx == null)
            return;

        if (ctx.RelicPhase == RelicPhases.CombatStart)
        {
            if (duration == AddValueBasedOnRollDuration.SameRoll)
                return;
            if (ctx.CombatManager == null)
                return;
            ctx.CombatManager.RegisterValueBasedRollWatcher(
                requiredFaceValues, bonusType, amount, burnDefinition, duration, fromRelicCombatStart: true);
            return;
        }

        if (ctx.RelicPhase == RelicPhases.AfterEnemyTurnPlayerTurnStart)
        {
            if (duration != AddValueBasedOnRollDuration.SameTurn)
                return;
            if (ctx.CombatManager == null)
                return;
            ctx.CombatManager.RegisterValueBasedRollWatcher(
                requiredFaceValues, bonusType, amount, burnDefinition, duration, fromRelicCombatStart: false);
            return;
        }

        if (duration != AddValueBasedOnRollDuration.SameRoll)
            return;

        AddValueBasedOnRollAction.ExecuteSameRollBonus(ctx, requiredFaceValues, bonusType, amount, burnDefinition);
    }
}
