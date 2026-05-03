using System;
using UnityEngine;

/// <summary>
/// When the settled face value matches <see cref="requiredFaceValue"/>, adds <see cref="amount"/> to armor or damage on that face,
/// or applies burn stacks. <see cref="duration"/> controls whether that happens only for this resolve, via watchers for later batches this turn, or for the whole combat.
/// </summary>
[Serializable]
public class AddValueBasedOnRollAction : GameActionWithIcon
{
    [Tooltip("Face value (after status modifiers) must equal this for the bonus to apply.")]
    [SerializeField] private int requiredFaceValue = 6;

    [SerializeField] private RollBonusType bonusType = RollBonusType.Armor;

    [Tooltip("Armor or damage added to the matching roll, or burn stacks when Bonus Type is Burn.")]
    [SerializeField] private int amount = 1;

    [Tooltip("Required when Bonus Type is Burn.")]
    [SerializeField] private BurnEffectSO burnDefinition;

    [SerializeField] private AddValueBasedOnRollDuration duration = AddValueBasedOnRollDuration.SameRoll;

    protected override ActionVisualId VisualKey => ActionVisualId.AddValueBasedOnRoll;

    public AddValueBasedOnRollDuration Duration => duration;
    public int RequiredFaceValueConfig => requiredFaceValue;
    public int AmountConfig => amount;
    public RollBonusType BonusTypeConfig => bonusType;
    public BurnEffectSO BurnDefinitionConfig => burnDefinition;

    public override void Execute(GameActionContext context)
    {
        if (duration == AddValueBasedOnRollDuration.SameRoll)
            ExecuteSameRollBonus(context, requiredFaceValue, bonusType, amount, burnDefinition);
    }

    /// <summary>Registers a watcher when <see cref="duration"/> is <see cref="AddValueBasedOnRollDuration.SameTurn"/> or <see cref="AddValueBasedOnRollDuration.EntireCombat"/> (called after the face's actions run).</summary>
    public void RegisterWatcherIfNeeded(CombatManager combat)
    {
        if (combat == null || duration == AddValueBasedOnRollDuration.SameRoll) return;
        combat.RegisterValueBasedRollWatcherFromDieResolution(requiredFaceValue, bonusType, amount, burnDefinition, duration);
    }

    /// <summary>Same-roll path for face actions and <see cref="RelicAddValueBasedOnRollAction"/> when <see cref="AddValueBasedOnRollDuration.SameRoll"/>.</summary>
    public static void ExecuteSameRollBonus(GameActionContext ctx, int requiredFaceValue, RollBonusType bonusType, int amount, BurnEffectSO burnDefinition)
    {
        if (ctx?.TriggeringFace == null) return;
        if (ctx.TriggeringFace.Value != requiredFaceValue) return;
        if (amount <= 0) return;

        if (!string.IsNullOrEmpty(ctx.RelicPhase))
        {
            if (bonusType == RollBonusType.Burn)
            {
                if (ctx.RelicPhase != RelicPhases.AfterPowerChangedFromRoll) return;
            }
            else
            {
                if (ctx.RelicPhase != RelicPhases.ModifyFaceResult) return;
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

        if (ctx?.Enemy == null) return;

        var applyStacks = amount;
        if (burnDefinition.target == StatusEffectTarget.Enemy && ctx.Player != null)
            applyStacks += ctx.Player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        var sctx = ctx.CombatManager != null
            ? ctx.CombatManager.BuildStatusContextForEffects()
            : new StatusEffectContext { Player = ctx.Player, Enemy = ctx.Enemy, CombatManager = ctx.CombatManager };

        ctx.Enemy.StatusEffects.ApplyStatus(burnDefinition, applyStacks, sctx);

        if (ctx.CombatManager != null)
            ctx.CombatManager.TurnRegistry?.RecordBurnApplied(applyStacks);

        if (GameActionDebug.Enabled)
            Debug.Log($"[AddValueBasedOnRoll] Burn +{applyStacks}");
    }

    /// <summary>Registers burn flyout row when this face is resolved (same timing as <see cref="ApplyStatusEffectAction"/>).</summary>
    public void AppendPoolContributionIfAny(FaceResult result, PlayerStatus player)
    {
        if (duration != AddValueBasedOnRollDuration.SameRoll) return;
        if (bonusType != RollBonusType.Burn || burnDefinition == null) return;
        TryAppendBurnPoolLine(result, player, requiredFaceValue, amount, burnDefinition);
    }

    public static void TryAppendBurnPoolLine(FaceResult result, PlayerStatus player, int requiredFaceValue, int amount, BurnEffectSO burnDefinition)
    {
        if (result == null || player == null) return;
        if (burnDefinition == null || amount <= 0) return;
        if (result.Value != requiredFaceValue) return;

        var applyStacks = amount;
        if (burnDefinition.target == StatusEffectTarget.Enemy)
            applyStacks += player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        if (applyStacks <= 0) return;

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = PoolRowKey.Custom(burnDefinition.name),
            Amount = applyStacks,
            Icon = GameIconCatalog.GetStatusIcon(burnDefinition)
        });
    }
}
