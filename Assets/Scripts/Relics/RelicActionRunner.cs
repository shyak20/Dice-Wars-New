using UnityEngine;

/// <summary>Runs <see cref="IGameAction"/> lists on all run relics for a given <see cref="GameActionContext.RelicPhase"/>.</summary>
public static class RelicActionRunner
{
    public static void RunPhase(CombatManager combat, string phase, FaceResult face = null)
    {
        if (combat == null) return;
        var ctx = combat.BuildRelicContext(face);
        ctx.RelicPhase = phase;
        ExecuteAllRelics(ctx);
    }

    public static int QueryIntSum(string phase, CombatManager combat = null)
    {
        var ctx = combat != null ? combat.BuildRelicContext(null) : BuildMapOnlyContext();
        ctx.RelicPhase = phase;
        ctx.RelicIntAccumulator = 0;
        ExecuteAllRelics(ctx);
        return ctx.RelicIntAccumulator;
    }

    /// <summary>
    /// Sum of relic contributions for <see cref="RelicPhases.QueryMaxPowerBonus"/> using deck data (map / UI without <see cref="CombatManager"/>).
    /// </summary>
    public static int QueryMaxPowerBonusFromRelics(PlayerDataSO playerData)
    {
        var ctx = new GameActionContext
        {
            PlayerData = playerData,
            RelicPhase = RelicPhases.QueryMaxPowerBonus,
            RelicIntAccumulator = 0
        };
        ExecuteAllRelics(ctx);
        return ctx.RelicIntAccumulator;
    }

    public static int QueryIntMax(string phase, CombatManager combat = null)
    {
        var ctx = combat != null ? combat.BuildRelicContext(null) : BuildMapOnlyContext();
        ctx.RelicPhase = phase;
        ctx.RelicIntAccumulator = 0;
        ExecuteAllRelics(ctx);
        return ctx.RelicIntAccumulator;
    }

    public static bool QueryBoolOr(string phase, CombatManager combat = null)
    {
        var ctx = combat != null ? combat.BuildRelicContext(null) : BuildMapOnlyContext();
        ctx.RelicPhase = phase;
        ctx.RelicBoolAccumulator = false;
        ExecuteAllRelics(ctx);
        return ctx.RelicBoolAccumulator;
    }

    public static bool TryConsumeFreeBust(CombatManager combat)
    {
        if (combat == null) return false;
        var ctx = combat.BuildRelicContext(null);
        ctx.RelicPhase = RelicPhases.TryConsumeFreeBust;
        ctx.RelicBoolAccumulator = false;
        ExecuteAllRelics(ctx);
        return ctx.RelicBoolAccumulator;
    }

    public static void ExecuteAllRelics(GameActionContext ctx)
    {
        var rm = RunManager.Instance;
        if (rm == null) return;

        foreach (var relic in rm.RunRelics)
        {
            if (relic?.actions == null) continue;
            ctx.SourceRelic = relic;
            foreach (var action in relic.actions)
            {
                if (action == null) continue;
                try
                {
                    action.Execute(ctx);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Relic action on '{relic.name}' threw: {e.Message}", relic);
                }
            }
        }

        ctx.SourceRelic = null;
    }

    static GameActionContext BuildMapOnlyContext() => new GameActionContext();
}
