using UnityEngine;

/// <summary>Shared apply logic for <see cref="CombatBonusChannel"/> bonuses on a resolving face or at turn end.</summary>
public static class CombatBonusChannelApplicator
{
    public static void ApplyToFaceResult(
        CombatBonusChannel channel,
        int amount,
        FaceResult result,
        CombatManager combat,
        BurnEffectSO burnDefinition,
        PoisonEffectSO poisonDefinition)
    {
        if (amount <= 0 || result == null || combat == null)
            return;

        switch (channel)
        {
            case CombatBonusChannel.Armor:
                result.Armor += amount;
                break;
            case CombatBonusChannel.Damage:
                result.Damage += amount;
                break;
            case CombatBonusChannel.Fire:
                ApplyFireToFaceResult(amount, result, combat, burnDefinition);
                break;
            case CombatBonusChannel.Poison:
                ApplyPoisonToFaceResult(amount, result, combat, poisonDefinition);
                break;
            default:
                Debug.LogError($"CombatBonusChannelApplicator: unsupported channel {channel}.");
                break;
        }
    }

    public static void ApplyDeferredTurnBonus(
        CombatBonusChannel channel,
        int amount,
        GameActionContext context,
        BurnEffectSO burnDefinition,
        PoisonEffectSO poisonDefinition)
    {
        if (amount <= 0 || context?.CombatManager == null)
            return;

        switch (channel)
        {
            case CombatBonusChannel.Armor:
                context.CombatManager.AddBonusArmorFromAction(amount);
                break;
            case CombatBonusChannel.Damage:
                context.CombatManager.AddBonusDamageFromAction(amount);
                break;
            case CombatBonusChannel.Fire:
                AddValueBasedOnRollAction.ApplyBurnToEnemyFromContext(context, amount, burnDefinition);
                break;
            case CombatBonusChannel.Poison:
                ApplyPoisonDeferred(context, amount, poisonDefinition);
                break;
            default:
                Debug.LogError($"CombatBonusChannelApplicator: unsupported channel {channel}.");
                break;
        }
    }

    static void ApplyFireToFaceResult(int amount, FaceResult result, CombatManager combat, BurnEffectSO burnDefinition)
    {
        if (burnDefinition == null)
        {
            Debug.LogError("CombatBonusChannelApplicator: assign burnDefinition for Fire.");
            return;
        }

        var ctx = BuildFaceContext(combat, result);
        AddValueBasedOnRollAction.ApplyBurnToEnemyFromContext(ctx, amount, burnDefinition);
        AppendStatusPoolContribution(result, burnDefinition, amount, ctx.Player);
    }

    static void ApplyPoisonToFaceResult(int amount, FaceResult result, CombatManager combat, PoisonEffectSO poisonDefinition)
    {
        if (poisonDefinition == null)
        {
            Debug.LogError("CombatBonusChannelApplicator: assign poisonDefinition for Poison.");
            return;
        }

        var ctx = BuildFaceContext(combat, result);
        ApplyPoisonDeferred(ctx, amount, poisonDefinition);
        AppendStatusPoolContribution(result, poisonDefinition, amount, ctx.Player);
    }

    static void ApplyPoisonDeferred(GameActionContext context, int stacks, PoisonEffectSO poisonDefinition)
    {
        if (poisonDefinition == null)
        {
            Debug.LogError("CombatBonusChannelApplicator: assign poisonDefinition for Poison.");
            return;
        }

        if (context.Enemy == null)
        {
            Debug.LogError("CombatBonusChannelApplicator: no enemy to apply poison.");
            return;
        }

        var statusCtx = context.CombatManager != null
            ? context.CombatManager.BuildStatusContextForEffects()
            : new StatusEffectContext
            {
                CombatManager = context.CombatManager,
                Player = context.Player,
                Enemy = context.Enemy,
            };

        context.Enemy.StatusEffects.ApplyStatus(poisonDefinition, stacks, statusCtx);
    }

    static GameActionContext BuildFaceContext(CombatManager combat, FaceResult result) =>
        new GameActionContext
        {
            CombatManager = combat,
            Player = combat.player,
            Enemy = combat.activeEnemy,
            TriggeringFace = result,
        };

    static void AppendStatusPoolContribution(
        FaceResult result,
        StatusEffectSO definition,
        int baseStacks,
        PlayerStatus player)
    {
        if (result == null || definition == null || baseStacks <= 0)
            return;

        var stacks = baseStacks;
        if (definition is BurnEffectSO burn && burn.target == StatusEffectTarget.Enemy && player != null)
            stacks += player.StatusEffects.GetStacks<PyromaniacEffectSO>();

        if (stacks <= 0)
            return;

        stacks = result.ApplyFireDoubleToEnemyBurnStacks(stacks, definition);

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = PoolRowKey.Custom(definition.name),
            Amount = stacks,
            Icon = GameIconCatalog.GetStatusIcon(definition),
            PerfectStrikeScales = true,
        });
    }
}
