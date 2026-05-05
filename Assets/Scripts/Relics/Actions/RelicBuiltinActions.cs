using System;
using UnityEngine;

static class RelicDeckUtil
{
    public static int CountEquippedFacesWithValue(PlayerDataSO data, int faceValue)
    {
        if (data?.currentDeck == null) return 0;
        var n = 0;
        foreach (var die in data.currentDeck)
        {
            if (die?.faces == null) continue;
            foreach (var f in die.faces)
            {
                if (f != null && f.value == faceValue)
                    n++;
            }
        }

        return n;
    }
}

[Serializable]
public sealed class RelicMaxPowerPerSixFaceAction : RelicGameActionBase
{
    [Tooltip("Added to max power for each face with value 6 on deck dice.")]
    public int bonusPerSixFace = 2;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.QueryMaxPowerBonus || ctx.PlayerData == null) return;
        var sixes = RelicDeckUtil.CountEquippedFacesWithValue(ctx.PlayerData, 6);
        ctx.RelicIntAccumulator += sixes * bonusPerSixFace;
    }
}

[Serializable]
public sealed class RelicPerfectAtMaxMinusOneAction : RelicGameActionBase
{
    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.QueryPerfectAtMaxMinusOne) return;
        ctx.RelicBoolAccumulator = true;
    }
}

[Serializable]
public sealed class RelicPermanentMapMovesAction : RelicGameActionBase
{
    public int bonusMoves = 2;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.QueryMapMoveBonus) return;
        ctx.RelicIntAccumulator += bonusMoves;
    }
}

[Serializable]
public sealed class RelicOncePerCombatFreeBustAction : RelicGameActionBase
{
    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.TryConsumeFreeBust) return;
        if (ctx.RelicRuntime == null || ctx.RelicRuntime.FreeBustConsumed) return;
        ctx.RelicRuntime.FreeBustConsumed = true;
        ctx.RelicBoolAccumulator = true;
    }
}

[Serializable]
public sealed class RelicGainRollsAtExactPowerAction : RelicGameActionBase
{
    public int powerTotal = 6;
    public int rollsGranted = 1;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.AfterPowerChangedFromRoll || ctx.CombatManager == null) return;
        if (ctx.CurrentPower != powerTotal) return;
        ctx.CombatManager.AddRollsRemaining(rollsGranted);
    }
}

[Serializable]
public sealed class RelicMaxHpOnPerfectAction : RelicGameActionBase
{
    public int maxHpGain = 1;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.OnPerfectStrike || ctx.Player == null) return;
        if (maxHpGain <= 0) return;
        ctx.Player.AddMaxHealthAndHeal(maxHpGain);
    }
}

[Serializable]
public sealed class RelicAddDamageOnFaceValueAction : RelicGameActionBase
{
    public int faceValue = 4;
    public int bonusDamage = 4;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.ModifyFaceResult || ctx.TriggeringFace == null) return;
        if (ctx.TriggeringFace.Value != faceValue) return;
        if (ctx.TriggeringFace.Damage > 0)
            ctx.TriggeringFace.Damage += bonusDamage;
    }
}

[Serializable]
public sealed class RelicAddArmorOnFaceValueAction : RelicGameActionBase
{
    public int faceValue = 3;
    public int bonusArmor = 3;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.ModifyFaceResult || ctx.TriggeringFace == null) return;
        if (ctx.TriggeringFace.Value != faceValue) return;
        ctx.TriggeringFace.Armor += bonusArmor;
    }
}

[Serializable]
public sealed class RelicApplyBurnOnFaceValueAction : RelicGameActionBase
{
    public int faceValue = 2;
    public int burnStacks = 2;
    public BurnEffectSO burnDefinition;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.AfterPowerChangedFromRoll) return;
        if (ctx.TriggeringFace == null || ctx.TriggeringFace.Value != faceValue) return;
        if (burnDefinition == null || ctx.Enemy == null) return;
        var stacks = burnStacks;
        if (ctx.TriggeringFace != null)
            stacks = ctx.TriggeringFace.ApplyFireDoubleToEnemyBurnStacks(stacks, burnDefinition);
        var sctx = ctx.CombatManager != null ? ctx.CombatManager.BuildStatusContextForEffects() : new StatusEffectContext { Player = ctx.Player, Enemy = ctx.Enemy, CombatManager = ctx.CombatManager };
        ctx.Enemy.StatusEffects.ApplyStatus(burnDefinition, stacks, sctx);
    }
}

[Serializable]
public sealed class RelicStartCombatStrengthAction : RelicGameActionBase
{
    public StrengthEffectSO strengthDefinition;
    public int stacks = 1;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.CombatStart || ctx.Player == null || strengthDefinition == null) return;
        if (stacks <= 0) return;
        var sctx = ctx.CombatManager != null ? ctx.CombatManager.BuildStatusContextForEffects() : new StatusEffectContext { Player = ctx.Player, Enemy = ctx.Enemy, CombatManager = ctx.CombatManager };
        ctx.Player.StatusEffects.ApplyStatus(strengthDefinition, stacks, sctx);
    }
}

[Serializable]
public sealed class RelicStrengthOnRollSixAction : RelicGameActionBase
{
    public StrengthEffectSO strengthDefinition;
    public int stacksPerRoll = 1;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.AfterPowerChangedFromRoll || ctx.Player == null) return;
        if (ctx.TriggeringFace == null || ctx.TriggeringFace.Value != 6) return;
        if (strengthDefinition == null || stacksPerRoll <= 0) return;
        var sctx = ctx.CombatManager != null ? ctx.CombatManager.BuildStatusContextForEffects() : new StatusEffectContext { Player = ctx.Player, Enemy = ctx.Enemy, CombatManager = ctx.CombatManager };
        ctx.Player.StatusEffects.ApplyStatus(strengthDefinition, stacksPerRoll, sctx);
    }
}

[Serializable]
public sealed class RelicDoubleNextFiveAfterFiveAction : RelicGameActionBase
{
    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.ModifyFaceResult || ctx.TriggeringFace == null || ctx.RelicRuntime == null)
            return;

        var v = ctx.TriggeringFace.Value;
        if (ctx.RelicRuntime.DoubleFivePrimed && v == 5 && ctx.TriggeringFace.Damage > 0)
        {
            ctx.TriggeringFace.Damage *= 2;
            ctx.RelicRuntime.DoubleFivePrimed = false;
        }
        else if (v == 5)
            ctx.RelicRuntime.DoubleFivePrimed = true;
    }
}

[Serializable]
public sealed class RelicPerfectStrikeMultiplierAction : RelicGameActionBase
{
    [Tooltip("Perfect strike uses at least this multiplier (e.g. 5 for ×5).")]
    public int multiplier = 5;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.QueryPerfectStrikeMultiplier) return;
        ctx.RelicIntAccumulator = Mathf.Max(ctx.RelicIntAccumulator, multiplier);
    }
}

[Serializable]
public sealed class RelicArmorBonusPerArmorDieAction : RelicGameActionBase
{
    public int bonusArmorPerArmorDie = 1;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.ModifyFaceResult || ctx.TriggeringFace == null || ctx.PlayerData == null) return;
        if (ctx.TriggeringFace.Type != DieType.Armor || ctx.TriggeringFace.Armor <= 0 || bonusArmorPerArmorDie <= 0) return;

        var armorDice = 0;
        foreach (var die in ctx.PlayerData.currentDeck)
        {
            if (die != null && die.dieType == DieType.Armor)
                armorDice++;
        }

        if (armorDice > 0)
            ctx.TriggeringFace.Armor += armorDice * bonusArmorPerArmorDie;
    }
}

[Serializable]
public sealed class RelicPerfectAtMaxPlusOneOncePerCombatAction : RelicGameActionBase
{
    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.QueryPerfectAtMaxPlusOne) return;
        if (ctx.RelicRuntime == null || ctx.RelicRuntime.PerfectAtMaxPlusOneConsumed) return;
        ctx.RelicRuntime.PerfectAtMaxPlusOneConsumed = true;
        ctx.RelicBoolAccumulator = true;
    }
}

[Serializable]
public sealed class RelicStormOrbBustShieldAction : RelicGameActionBase
{
    [Min(1)] public int startingStacks = 3;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.TryConsumeFreeBust) return;
        if (ctx.RelicRuntime == null || startingStacks <= 0) return;

        if (ctx.RelicRuntime.StormOrbStacksRemaining < 0)
            ctx.RelicRuntime.StormOrbStacksRemaining = startingStacks;

        if (ctx.RelicRuntime.StormOrbStacksRemaining <= 0)
            return;

        ctx.RelicRuntime.StormOrbStacksRemaining--;
        ctx.RelicBoolAccumulator = true;
        if (ctx.RelicRuntime.StormOrbStacksRemaining <= 0)
            ctx.RelicRuntime.PendingDestroyRelicAfterBust = ctx.SourceRelic;
    }
}

[Serializable]
public sealed class RelicSkipFirstRollPowerEachTurnAction : RelicGameActionBase
{
    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicRuntime == null) return;
        if (ctx.RelicPhase == RelicPhases.CombatStart || ctx.RelicPhase == RelicPhases.AfterEnemyTurnPlayerTurnStart)
        {
            ctx.RelicRuntime.RuneTabletConsumedThisTurn = false;
            return;
        }

        if (ctx.RelicPhase != RelicPhases.ModifyFaceResult || ctx.TriggeringFace == null || ctx.CombatManager == null)
            return;

        if (!ctx.CombatManager.IsResolvingFirstRollOfTurn() || ctx.RelicRuntime.RuneTabletConsumedThisTurn)
            return;

        ctx.TriggeringFace.PowerContributionThisResolve = 0;
        ctx.RelicRuntime.RuneTabletConsumedThisTurn = true;
    }
}

[Serializable]
public sealed class RelicPermanentRollsPerTurnBonusAction : RelicGameActionBase
{
    public int bonusRolls = 1;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.QueryMaxRollsBonus) return;
        ctx.RelicIntAccumulator += bonusRolls;
    }
}

[Serializable]
public sealed class RelicMaxHpOnCombatVictoryAction : RelicGameActionBase
{
    public int maxHpGain = 2;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.OnCombatVictory || ctx.Player == null || maxHpGain <= 0) return;
        ctx.Player.AddMaxHealthAndHeal(maxHpGain);
    }
}

[Serializable]
public sealed class RelicHealOnCombatVictoryAction : RelicGameActionBase
{
    public int healAmount = 2;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.OnCombatVictory || ctx.Player == null || healAmount <= 0) return;
        ctx.Player.Heal(healAmount);
    }
}

[Serializable]
public sealed class RelicDamageBelowHpPercentAction : RelicGameActionBase
{
    [Range(1f, 100f)] public float hpThresholdPercent = 50f;
    [Min(2)] public int multiplier = 2;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.ModifyFaceResult || ctx.TriggeringFace == null || ctx.Player == null) return;
        if (ctx.TriggeringFace.Type != DieType.Damage || ctx.TriggeringFace.Damage <= 0) return;

        var maxHp = Mathf.Max(1, ctx.Player.maxHealth);
        var currentHpPercent = (float)ctx.Player.GetCurrentHealth() / maxHp * 100f;
        if (currentHpPercent <= hpThresholdPercent)
            ctx.TriggeringFace.Damage *= Mathf.Max(2, multiplier);
    }
}

[Serializable]
public sealed class RelicAddValueOnFaceListAction : RelicGameActionBase
{
    public int[] requiredFaceValues = { 2, 4, 6 };
    public RollBonusType bonusType = RollBonusType.Damage;
    public int amount = 2;
    public BurnEffectSO burnDefinition;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.TriggeringFace == null) return;
        if (bonusType == RollBonusType.Burn)
        {
            if (ctx.RelicPhase != RelicPhases.AfterPowerChangedFromRoll) return;
        }
        else
        {
            if (ctx.RelicPhase != RelicPhases.ModifyFaceResult) return;
        }

        if (requiredFaceValues == null || requiredFaceValues.Length == 0 || amount == 0) return;

        var match = false;
        for (var i = 0; i < requiredFaceValues.Length; i++)
        {
            if (ctx.TriggeringFace.Value == requiredFaceValues[i])
            {
                match = true;
                break;
            }
        }

        if (!match) return;

        AddValueBasedOnRollAction.ExecuteSameRollBonus(ctx, ctx.TriggeringFace.Value, bonusType, amount, burnDefinition);
    }
}

[Serializable]
public sealed class RelicShopDiscountPercentAction : RelicGameActionBase
{
    [Range(1, 95)] public int discountPercent = 20;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.QueryShopDiscountPercent) return;
        ctx.RelicIntAccumulator = Mathf.Max(ctx.RelicIntAccumulator, discountPercent);
    }
}

[Serializable]
public sealed class RelicMapCorruptionDamageReductionPercentAction : RelicGameActionBase
{
    [Range(1, 100)] public int reductionPercent = 25;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.QueryMapCorruptionDamageReductionPercent) return;
        ctx.RelicIntAccumulator = Mathf.Max(ctx.RelicIntAccumulator, reductionPercent);
    }
}

[Serializable]
public sealed class RelicBonusGoldOnCombatVictoryAction : RelicGameActionBase
{
    [Min(1)] public int bonusGold = 10;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.OnCombatVictory || bonusGold <= 0) return;
        VictoryRewardBuffer.PendingGold += bonusGold;
    }
}

[Serializable]
public sealed class RelicArmorFromRemainingRollsAction : RelicGameActionBase
{
    public int armorPerRemainingRoll = 2;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.BeforeSubmitTurn || ctx.Player == null || ctx.CombatManager == null) return;
        if (armorPerRemainingRoll <= 0) return;
        var remaining = ctx.CombatManager.GetRollsRemaining();
        if (remaining <= 0) return;
        ctx.Player.AddArmor(remaining * armorPerRemainingRoll);
    }
}

[Serializable]
public sealed class RelicBurnOnEveryFireRollAction : RelicGameActionBase
{
    public BurnEffectSO burnDefinition;
    public int burnStacks = 1;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.AfterPowerChangedFromRoll || ctx.TriggeringFace == null) return;
        if (ctx.TriggeringFace.Type != DieType.Fire || burnDefinition == null || burnStacks <= 0 || ctx.Enemy == null) return;

        var stacks = ctx.TriggeringFace.ApplyFireDoubleToEnemyBurnStacks(burnStacks, burnDefinition);
        var sctx = ctx.CombatManager != null
            ? ctx.CombatManager.BuildStatusContextForEffects()
            : new StatusEffectContext { Player = ctx.Player, Enemy = ctx.Enemy, CombatManager = ctx.CombatManager };
        ctx.Enemy.StatusEffects.ApplyStatus(burnDefinition, stacks, sctx);
    }
}

[Serializable]
public sealed class RelicStrengthOnSpecificFaceValueAction : RelicGameActionBase
{
    public int faceValue = 3;
    public StrengthEffectSO strengthDefinition;
    public int stacksPerRoll = 1;

    public override void Execute(GameActionContext ctx)
    {
        if (ctx.RelicPhase != RelicPhases.AfterPowerChangedFromRoll || ctx.Player == null) return;
        if (ctx.TriggeringFace == null || ctx.TriggeringFace.Value != faceValue) return;
        if (strengthDefinition == null || stacksPerRoll <= 0) return;
        var sctx = ctx.CombatManager != null
            ? ctx.CombatManager.BuildStatusContextForEffects()
            : new StatusEffectContext { Player = ctx.Player, Enemy = ctx.Enemy, CombatManager = ctx.CombatManager };
        ctx.Player.StatusEffects.ApplyStatus(strengthDefinition, stacksPerRoll, sctx);
    }
}
