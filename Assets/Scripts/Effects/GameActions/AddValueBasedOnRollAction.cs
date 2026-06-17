using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// When the settled face value is in <see cref="requiredFaceValues"/>, adds <see cref="amount"/> to armor or damage on that face,
/// or applies burn stacks. <see cref="duration"/> controls same-roll vs rest-of-turn watcher behavior (see <see cref="AddValueBasedOnRollDuration"/>).
/// </summary>
[Serializable]
public class AddValueBasedOnRollAction : GameActionWithIcon, ISerializationCallbackReceiver
{
    [SerializeField, HideInInspector, FormerlySerializedAs("requiredFaceValue")]
    private int legacyRequiredFaceValue;

    [SerializeField, HideInInspector, FormerlySerializedAs("requiredFaceValues")]
    private int[] legacyRequiredFaceValuesArray;

    [SerializeField, HideInInspector, FormerlySerializedAs("bonusOnAnyDieRollRestOfTurn")]
    private int legacyBonusOnAnyDieRollRestOfTurn;

    [SerializeField, Tooltip("Face values (after modifiers) that activate this bonus.")]
    private FaceValueMatchSet requiredFaceValues = new FaceValueMatchSet();

    [SerializeField] private RollBonusType bonusType = RollBonusType.Armor;

    [Tooltip("Bonus magnitude: same-roll grant and/or rest-of-turn watcher (see Duration).")]
    [SerializeField] private int amount = 1;

    [Tooltip("Required when Bonus Type is Burn.")]
    [SerializeField] private BurnEffectSO burnDefinition;

    [SerializeField] private AddValueBasedOnRollDuration duration = AddValueBasedOnRollDuration.SameRoll;

    protected override ActionVisualId VisualKey => ActionVisualId.AddValueBasedOnRoll;

    public AddValueBasedOnRollDuration Duration => duration;

    public void OnBeforeSerialize() => MigrateSerializedFields();

    public void OnAfterDeserialize() => MigrateSerializedFields();

    void MigrateSerializedFields()
    {
        requiredFaceValues ??= new FaceValueMatchSet();
        requiredFaceValues.MigrateLegacySingleValue(legacyRequiredFaceValue);
        legacyRequiredFaceValue = 0;
        requiredFaceValues.MigrateLegacyIntArray(legacyRequiredFaceValuesArray);
        legacyRequiredFaceValuesArray = null;

        if (legacyBonusOnAnyDieRollRestOfTurn > 0)
        {
            if (amount <= 0)
                amount = legacyBonusOnAnyDieRollRestOfTurn;
            if (duration == AddValueBasedOnRollDuration.SameRoll)
                duration = AddValueBasedOnRollDuration.SameTurn;
            legacyBonusOnAnyDieRollRestOfTurn = 0;
        }

        // Removed enum values SameTurnAnyDie (3) / EntireCombatAnyDie (4) — fold into SameTurn / EntireCombat.
        var durationRaw = (int)duration;
        if (durationRaw == 3)
            duration = AddValueBasedOnRollDuration.SameTurn;
        else if (durationRaw == 4)
            duration = AddValueBasedOnRollDuration.EntireCombat;
    }

    FaceValueMatchSet GetRequiredFaceValues()
    {
        MigrateSerializedFields();
        return requiredFaceValues;
    }

    public override void Execute(GameActionContext context)
    {
        if (duration == AddValueBasedOnRollDuration.SameRoll)
            ExecuteSameRollBonus(context, GetRequiredFaceValues(), bonusType, amount, burnDefinition);
    }

    /// <summary>Registers watchers when <see cref="duration"/> is a rest-of-turn / entire-combat mode (called after the face resolves).</summary>
    public void RegisterWatcherIfNeeded(CombatManager combat, FaceResult resolvingFace)
    {
        if (combat == null || resolvingFace == null)
            return;

        var required = GetRequiredFaceValues();
        if (!required.Matches(resolvingFace.Value))
            return;

        if (duration == AddValueBasedOnRollDuration.SameRoll)
            return;

        if (amount <= 0)
            return;

        combat.RegisterValueBasedRollWatcherAnyDieFromDieResolution(
            amount, bonusType, burnDefinition, duration);
    }

    /// <summary>Same-roll path for face actions and relic hooks when <see cref="AddValueBasedOnRollDuration.SameRoll"/>.</summary>
    public static void ExecuteSameRollBonus(
        GameActionContext ctx,
        FaceValueMatchSet requiredValues,
        RollBonusType bonusType,
        int amount,
        BurnEffectSO burnDefinition)
    {
        if (ctx?.TriggeringFace == null)
            return;
        if (requiredValues == null || !requiredValues.Matches(ctx.TriggeringFace.Value))
            return;
        if (amount <= 0)
            return;

        if (!string.IsNullOrEmpty(ctx.RelicPhase))
        {
            if (bonusType == RollBonusType.Burn)
            {
                if (ctx.RelicPhase != RelicPhases.AfterPowerChangedFromRoll)
                    return;
            }
            else
            {
                if (ctx.RelicPhase != RelicPhases.ModifyFaceResult)
                    return;
            }
        }

        switch (bonusType)
        {
            case RollBonusType.Armor:
                ctx.TriggeringFace.Armor += amount;
                break;
            case RollBonusType.Damage:
                ctx.TriggeringFace.Damage += amount;
                break;
            case RollBonusType.Burn:
                ApplyBurnToEnemyFromContext(ctx, amount, burnDefinition);
                break;
        }
    }

    public static void ApplyBurnToEnemyFromContext(GameActionContext ctx, int amount, BurnEffectSO burnDefinition)
    {
        if (burnDefinition == null)
        {
            Debug.LogError("AddValueBasedOnRollAction: assign burnDefinition when Bonus Type is Burn.");
            return;
        }

        if (ctx?.Enemy == null)
            return;

        var applyStacks = amount;
        if (burnDefinition.target == StatusEffectTarget.Enemy && ctx.Player != null)
            applyStacks += ctx.Player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        if (ctx.TriggeringFace != null)
            applyStacks = ctx.TriggeringFace.ApplyFireDoubleToEnemyBurnStacks(applyStacks, burnDefinition);

        var sctx = ctx.CombatManager != null
            ? ctx.CombatManager.BuildStatusContextForEffects()
            : new StatusEffectContext { Player = ctx.Player, Enemy = ctx.Enemy, CombatManager = ctx.CombatManager };

        ctx.Enemy.StatusEffects.ApplyStatus(burnDefinition, applyStacks, sctx);

        if (ctx.CombatManager != null)
            ctx.CombatManager.TurnRegistry?.RecordBurnApplied(applyStacks);

        if (GameActionDebug.Enabled)
            Debug.Log($"[AddValueBasedOnRoll] Burn +{applyStacks}");
    }

    public void AppendPoolContributionIfAny(FaceResult result, PlayerStatus player)
    {
        if (duration != AddValueBasedOnRollDuration.SameRoll)
            return;
        if (bonusType != RollBonusType.Burn || burnDefinition == null)
            return;
        TryAppendBurnPoolLine(result, player, GetRequiredFaceValues(), amount, burnDefinition);
    }

    public static void TryAppendBurnPoolLineForWatcher(
        FaceResult result,
        PlayerStatus player,
        System.Collections.Generic.IReadOnlyList<int> requiredValues,
        bool matchAnyFaceValue,
        int amount,
        BurnEffectSO burnDefinition)
    {
        if (result == null || player == null)
            return;
        if (burnDefinition == null || amount <= 0)
            return;
        if (!FaceValueMatchSet.MatchesAny(result.Value, requiredValues, matchAnyFaceValue))
            return;

        AppendBurnPoolLineCore(result, player, amount, burnDefinition);
    }

    public static void TryAppendBurnPoolLine(
        FaceResult result,
        PlayerStatus player,
        FaceValueMatchSet requiredValues,
        int amount,
        BurnEffectSO burnDefinition)
    {
        if (result == null || player == null)
            return;
        if (burnDefinition == null || amount <= 0)
            return;
        if (requiredValues == null || !requiredValues.Matches(result.Value))
            return;

        AppendBurnPoolLineCore(result, player, amount, burnDefinition);
    }

    static void AppendBurnPoolLineCore(
        FaceResult result,
        PlayerStatus player,
        int amount,
        BurnEffectSO burnDefinition)
    {
        var applyStacks = amount;
        if (burnDefinition.target == StatusEffectTarget.Enemy)
            applyStacks += player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        if (applyStacks <= 0)
            return;

        applyStacks = result.ApplyFireDoubleToEnemyBurnStacks(applyStacks, burnDefinition);

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = PoolRowKey.Custom(burnDefinition.name),
            Amount = applyStacks,
            Icon = GameIconCatalog.GetStatusIcon(burnDefinition)
        });
    }

#if UNITY_EDITOR
    private void OnValidate() => MigrateSerializedFields();
#endif
}
