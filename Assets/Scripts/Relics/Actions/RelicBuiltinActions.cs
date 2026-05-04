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
