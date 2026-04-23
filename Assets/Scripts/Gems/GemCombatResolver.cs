using UnityEngine;

/// <summary>Applies socketed <see cref="GemSO"/> when a die resolves a face.</summary>
public static class GemCombatResolver
{
    const int DefaultBonusRollChainCapPerTurn = 3;
    const string GemPoolRowBurn = "Gem Burn";
    const string GemPoolRowHeal = "Gem Heal";
    const string GemPoolRowCleanse = "Gem Cleanse";
    const string GemPoolRowGold = "Gem Gold";
    const string GemPoolRowMaxHp = "Gem Max HP";
    const string GemPoolRowPower = "Gem Power";

    public static void ApplySocketedGems(DieAssetSO die, FaceResult result, CombatManager combat)
    {
        if (die == null || result == null || combat == null)
            return;

        foreach (var gem in die.GetSocketedGems())
        {
            if (gem == null)
                continue;
            if (!gem.MatchesRolledValue(result.Value))
                continue;
            ApplyGemEffects(gem, result, combat);
        }
    }

    private static void ApplyGemEffects(GemSO gem, FaceResult result, CombatManager combat)
    {
        if (gem.effects == null || gem.effects.Count == 0)
            return;

        var ctx = combat.BuildGameActionContextForFace(result);

        foreach (var entry in gem.effects)
        {
            if (entry == null)
                continue;
            if (entry.activateImmediately)
                ApplyImmediateGemEffectEntry(entry, gem, result, combat, ctx);
            else
                ApplyDeferredGemEffectEntry(entry, gem, result, combat, ctx);
        }
    }

    private static void ApplyImmediateGemEffectEntry(
        GemEffectEntry entry,
        GemSO gem,
        FaceResult result,
        CombatManager combat,
        GameActionContext ctx)
    {
        switch (entry.kind)
        {
            case GemEffectKind.HealPlayer:
                if (entry.param > 0 && combat.player != null)
                    combat.player.Heal(entry.param);
                break;

            case GemEffectKind.GrantExtraRollsThisTurn:
                combat.AddRollsRemaining(Mathf.Max(0, entry.param));
                break;

            case GemEffectKind.ApplyBurnToEnemy:
                if (entry.burnDefinition == null)
                {
                    Debug.LogError($"Gem '{gem.name}': ApplyBurnToEnemy row needs burnDefinition.", gem);
                    break;
                }

                AddValueBasedOnRollAction.ApplyBurnToEnemyFromContext(ctx, entry.param, entry.burnDefinition);
                AddValueBasedOnRollAction.TryAppendBurnPoolLine(
                    result, combat.player, result.Value, entry.param, entry.burnDefinition);
                break;

            case GemEffectKind.IncreasePowerMeter:
                combat.AddResonancePower(entry.param);
                break;

            case GemEffectKind.BonusRollsThisTurnCapped:
                combat.TryApplyGemBonusRollChain(Mathf.Max(0, entry.param), DefaultBonusRollChainCapPerTurn);
                break;

            case GemEffectKind.CleanseRandomDebuff:
                if (entry.param <= 0 || combat.player == null)
                    break;
                var cleanseStacks = entry.param;
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalStacks = cleanseStacks * endCtx.CombatManager.GetAppliedMultiplier();
                    var statusCtx = new StatusEffectContext
                    {
                        CombatManager = endCtx.CombatManager,
                        Player = endCtx.Player,
                        Enemy = endCtx.Enemy
                    };
                    endCtx.Player.StatusEffects.ReduceRandomDebuffStacks(finalStacks, statusCtx);
                });
                break;

            case GemEffectKind.GrantGold:
                if (entry.param > 0 && RunEconomyManager.Instance != null)
                    RunEconomyManager.Instance.GrantGold(entry.param, null);
                break;

            case GemEffectKind.AddMaxHpOnly:
                if (entry.param > 0 && combat.player != null)
                    combat.player.AddMaxHP(entry.param);
                break;

            case GemEffectKind.AddArmorToThisFace:
                result.Armor += Mathf.Max(0, entry.param);
                break;

            case GemEffectKind.AddDamageToThisFace:
                if (result.Type == DieType.Damage)
                    result.Damage += Mathf.Max(0, entry.param);
                break;

            case GemEffectKind.MultiplyPhysicalDamageThisFace:
                if (result.Type != DieType.Damage || result.Damage <= 0)
                    break;
                var mult = entry.param < 2 ? 2 : entry.param;
                result.Damage *= mult;
                break;

            case GemEffectKind.FreePlayerRollsForThisDie:
                break;
        }
    }

    private static void ApplyDeferredGemEffectEntry(
        GemEffectEntry entry,
        GemSO gem,
        FaceResult result,
        CombatManager combat,
        GameActionContext ctx)
    {
        switch (entry.kind)
        {
            case GemEffectKind.HealPlayer:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0 || combat.player == null) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowHeal),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.Heal),
                    PerfectStrikeScales = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = baseAmount * Mathf.Max(1, endCtx.CombatManager.GetAppliedMultiplier());
                    endCtx.Player.Heal(finalAmount);
                });
                break;
            }
            case GemEffectKind.ApplyBurnToEnemy:
            {
                if (entry.burnDefinition == null)
                {
                    Debug.LogError($"Gem '{gem.name}': ApplyBurnToEnemy row needs burnDefinition.", gem);
                    break;
                }
                var baseStacks = Mathf.Max(0, entry.param);
                if (baseStacks <= 0) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowBurn),
                    Amount = baseStacks,
                    Icon = GameIconCatalog.GetStatusIcon(entry.burnDefinition),
                    PerfectStrikeScales = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalStacks = baseStacks * Mathf.Max(1, endCtx.CombatManager.GetAppliedMultiplier());
                    AddValueBasedOnRollAction.ApplyBurnToEnemyFromContext(ctx, finalStacks, entry.burnDefinition);
                });
                break;
            }
            case GemEffectKind.IncreasePowerMeter:
            {
                var baseAmount = entry.param;
                if (baseAmount == 0) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowPower),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.AddPower),
                    PerfectStrikeScales = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = baseAmount * Mathf.Max(1, endCtx.CombatManager.GetAppliedMultiplier());
                    endCtx.CombatManager.AddResonancePower(finalAmount);
                });
                break;
            }
            case GemEffectKind.CleanseRandomDebuff:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0 || combat.player == null) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowCleanse),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.Cleanse),
                    PerfectStrikeScales = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalStacks = baseAmount * Mathf.Max(1, endCtx.CombatManager.GetAppliedMultiplier());
                    var statusCtx = new StatusEffectContext
                    {
                        CombatManager = endCtx.CombatManager,
                        Player = endCtx.Player,
                        Enemy = endCtx.Enemy
                    };
                    endCtx.Player.StatusEffects.ReduceRandomDebuffStacks(finalStacks, statusCtx);
                });
                break;
            }
            case GemEffectKind.GrantGold:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowGold),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.AddValueBasedOnRoll),
                    PerfectStrikeScales = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = baseAmount * Mathf.Max(1, endCtx.CombatManager.GetAppliedMultiplier());
                    if (finalAmount > 0 && RunEconomyManager.Instance != null)
                        RunEconomyManager.Instance.GrantGold(finalAmount, null);
                });
                break;
            }
            case GemEffectKind.AddMaxHpOnly:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0 || combat.player == null) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowMaxHp),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.MaxHp),
                    PerfectStrikeScales = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = baseAmount * Mathf.Max(1, endCtx.CombatManager.GetAppliedMultiplier());
                    endCtx.Player.AddMaxHP(finalAmount);
                });
                break;
            }
            case GemEffectKind.AddArmorToThisFace:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.FromDieType(DieType.Armor),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetElementIcon(DieType.Armor),
                    PerfectStrikeScales = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = baseAmount * Mathf.Max(1, endCtx.CombatManager.GetAppliedMultiplier());
                    endCtx.CombatManager.AddBonusArmorFromAction(finalAmount);
                });
                break;
            }
            case GemEffectKind.AddDamageToThisFace:
            {
                if (result.Type != DieType.Damage) break;
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.FromDieType(DieType.Damage),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetElementIcon(DieType.Damage),
                    PerfectStrikeScales = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = baseAmount * Mathf.Max(1, endCtx.CombatManager.GetAppliedMultiplier());
                    endCtx.CombatManager.AddBonusDamageFromAction(finalAmount);
                });
                break;
            }
            case GemEffectKind.GrantExtraRollsThisTurn:
            case GemEffectKind.BonusRollsThisTurnCapped:
            case GemEffectKind.FreePlayerRollsForThisDie:
            case GemEffectKind.MultiplyPhysicalDamageThisFace:
                // These effects alter roll pipeline behavior and cannot be safely deferred to turn-end.
                Debug.LogWarning($"Gem '{gem.name}': '{entry.kind}' cannot be deferred; applying immediately.");
                ApplyImmediateGemEffectEntry(entry, gem, result, combat, ctx);
                break;
        }
    }
}
