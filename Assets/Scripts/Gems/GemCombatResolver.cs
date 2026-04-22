using UnityEngine;

/// <summary>Applies socketed <see cref="GemSO"/> when a die resolves a face.</summary>
public static class GemCombatResolver
{
    const int DefaultBonusRollChainCapPerTurn = 3;

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
            ApplyGemEffectEntry(entry, gem, result, combat, ctx);
        }
    }

    private static void ApplyGemEffectEntry(
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
}
