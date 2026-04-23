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
    static int _nextDeferredHandleId = 1;

    private static int NextDeferredHandleId() => _nextDeferredHandleId++;

    sealed class GemResolveState
    {
        public int AddedDamageBonusSoFar;
        public int PendingDamageMultiplier = 1;
    }

    public static void ApplySocketedGems(DieAssetSO die, FaceResult result, CombatManager combat)
    {
        if (die == null || result == null || combat == null)
            return;

        var state = new GemResolveState();
        foreach (var gem in die.GetSocketedGems())
        {
            if (gem == null)
                continue;
            if (!gem.MatchesRolledValue(result.Value))
                continue;
            ApplyGemEffects(die, gem, result, combat, state);
        }

        FinalizePendingDamageMultiplier(result, combat, state);
    }

    private static void ApplyGemEffects(DieAssetSO sourceDie, GemSO gem, FaceResult result, CombatManager combat, GemResolveState state)
    {
        if (gem.effects == null || gem.effects.Count == 0)
            return;

        var ctx = combat.BuildGameActionContextForFace(result);

        foreach (var entry in gem.effects)
        {
            if (entry == null)
                continue;
            if (entry.activateImmediately)
                ApplyImmediateGemEffectEntry(sourceDie, entry, gem, result, combat, ctx, state);
            else
                ApplyDeferredGemEffectEntry(sourceDie, entry, gem, result, combat, ctx, state);
        }
    }

    private static void FinalizePendingDamageMultiplier(FaceResult result, CombatManager combat, GemResolveState state)
    {
        if (state == null) return;
        if (state.PendingDamageMultiplier <= 1) return;

        var mult = state.PendingDamageMultiplier;

        if (result.Type == DieType.Damage && result.Damage > 0)
            result.Damage *= mult;

        if (state.AddedDamageBonusSoFar > 0)
        {
            var extraFromMultiplier = state.AddedDamageBonusSoFar * (mult - 1);
            combat.AddBonusDamageFromAction(extraFromMultiplier);

            // Visual-only line so flyout mode reflects multiplied gem-added pending damage.
            result.ActionPoolContributions.Add(new FacePoolExtraContribution
            {
                PoolKey = PoolRowKey.FromDieType(DieType.Damage),
                Amount = extraFromMultiplier,
                Icon = GameIconCatalog.GetElementIcon(DieType.Damage),
                VisualFlyoutOnly = true
            });
        }
    }

    private static void ApplyImmediateGemEffectEntry(
        DieAssetSO sourceDie,
        GemEffectEntry entry,
        GemSO gem,
        FaceResult result,
        CombatManager combat,
        GameActionContext ctx,
        GemResolveState state)
    {
        switch (entry.kind)
        {
            case GemEffectKind.HealPlayer:
                if (entry.param > 0 && combat.player != null)
                    combat.player.Heal(entry.param);
                break;

            case GemEffectKind.GrantExtraRollsThisTurn:
                combat.TryApplyGemExtraRollsPerDie(sourceDie, Mathf.Max(0, entry.param), 2);
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
            {
                var amount = Mathf.Max(0, entry.param);
                if (amount <= 0) break;
                // Real pending damage source of truth (works for all die types).
                combat.AddBonusDamageFromAction(amount);
                state.AddedDamageBonusSoFar += amount;
                // Flyout-only line so flyout-increment UI shows the gain immediately, then resyncs to combat truth.
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.FromDieType(DieType.Damage),
                    Amount = amount,
                    Icon = GameIconCatalog.GetElementIcon(DieType.Damage),
                    VisualFlyoutOnly = true
                });
                break;
            }

            case GemEffectKind.MultiplyPhysicalDamageThisFace:
            {
                var mult = entry.param < 2 ? 2 : entry.param;
                state.PendingDamageMultiplier *= mult;
                break;
            }

            case GemEffectKind.FreeFirstRollForThisDie:
                if (sourceDie != null && combat.TryConsumeGemNoPowerOnMatchCharge(sourceDie))
                    result.PowerContributionThisResolve = 0;
                break;
        }
    }

    private static void ApplyDeferredGemEffectEntry(
        DieAssetSO sourceDie,
        GemEffectEntry entry,
        GemSO gem,
        FaceResult result,
        CombatManager combat,
        GameActionContext ctx,
        GemResolveState state)
    {
        switch (entry.kind)
        {
            case GemEffectKind.HealPlayer:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0 || combat.player == null) break;
                var handle = NextDeferredHandleId();
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowHeal),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.Heal),
                    PerfectStrikeScales = true,
                    GemDeferredHandleId = handle,
                    CancelOnBustNullifyArmor = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = endCtx.CombatManager.ResolveGemDeferredPoolAmount(handle);
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
                var handle = NextDeferredHandleId();
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowBurn),
                    Amount = baseStacks,
                    Icon = GameIconCatalog.GetStatusIcon(entry.burnDefinition),
                    PerfectStrikeScales = true,
                    GemDeferredHandleId = handle,
                    CancelOnBustNullifyDamage = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalStacks = endCtx.CombatManager.ResolveGemDeferredPoolAmount(handle);
                    AddValueBasedOnRollAction.ApplyBurnToEnemyFromContext(ctx, finalStacks, entry.burnDefinition);
                });
                break;
            }
            case GemEffectKind.IncreasePowerMeter:
            {
                var baseAmount = entry.param;
                if (baseAmount == 0) break;
                var handle = NextDeferredHandleId();
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowPower),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.AddPower),
                    PerfectStrikeScales = true,
                    GemDeferredHandleId = handle,
                    CancelOnBustNullifyDamage = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = endCtx.CombatManager.ResolveGemDeferredPoolAmount(handle);
                    endCtx.CombatManager.AddResonancePower(finalAmount);
                });
                break;
            }
            case GemEffectKind.CleanseRandomDebuff:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0 || combat.player == null) break;
                var handle = NextDeferredHandleId();
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowCleanse),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.Cleanse),
                    PerfectStrikeScales = true,
                    GemDeferredHandleId = handle,
                    CancelOnBustNullifyArmor = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalStacks = endCtx.CombatManager.ResolveGemDeferredPoolAmount(handle);
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
                var handle = NextDeferredHandleId();
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowGold),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.AddValueBasedOnRoll),
                    PerfectStrikeScales = true,
                    GemDeferredHandleId = handle,
                    CancelOnBustNullifyArmor = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = endCtx.CombatManager.ResolveGemDeferredPoolAmount(handle);
                    if (finalAmount > 0 && RunEconomyManager.Instance != null)
                        RunEconomyManager.Instance.GrantGold(finalAmount, null);
                });
                break;
            }
            case GemEffectKind.AddMaxHpOnly:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0 || combat.player == null) break;
                var handle = NextDeferredHandleId();
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.Custom(GemPoolRowMaxHp),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetActionIcon(ActionVisualId.MaxHp),
                    PerfectStrikeScales = true,
                    GemDeferredHandleId = handle,
                    CancelOnBustNullifyArmor = true
                });
                combat.QueueTurnEndAction(endCtx =>
                {
                    var finalAmount = endCtx.CombatManager.ResolveGemDeferredPoolAmount(handle);
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
                    // Flyout-only row: real pending armor is tracked via bonusArmorFromActions.
                    VisualFlyoutOnly = true
                });
                combat.AddBonusArmorFromAction(baseAmount);
                break;
            }
            case GemEffectKind.AddDamageToThisFace:
            {
                var baseAmount = Mathf.Max(0, entry.param);
                if (baseAmount <= 0) break;
                result.ActionPoolContributions.Add(new FacePoolExtraContribution
                {
                    PoolKey = PoolRowKey.FromDieType(DieType.Damage),
                    Amount = baseAmount,
                    Icon = GameIconCatalog.GetElementIcon(DieType.Damage),
                    // Flyout-only row: real pending damage is tracked via bonusDamageFromActions.
                    VisualFlyoutOnly = true
                });
                combat.AddBonusDamageFromAction(baseAmount);
                state.AddedDamageBonusSoFar += baseAmount;
                break;
            }
            case GemEffectKind.GrantExtraRollsThisTurn:
            case GemEffectKind.BonusRollsThisTurnCapped:
            case GemEffectKind.FreeFirstRollForThisDie:
            case GemEffectKind.MultiplyPhysicalDamageThisFace:
                // These effects alter roll pipeline behavior and cannot be safely deferred to turn-end.
                Debug.LogWarning($"Gem '{gem.name}': '{entry.kind}' cannot be deferred; applying immediately.");
                ApplyImmediateGemEffectEntry(sourceDie, entry, gem, result, combat, ctx, state);
                break;
        }
    }
}
