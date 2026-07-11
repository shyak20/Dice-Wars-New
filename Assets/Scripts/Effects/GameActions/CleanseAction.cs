using System;
using UnityEngine;

[Serializable]
public class CleanseAction : GameActionWithIcon
{
    [SerializeField] private int stacks = 1;

    [Tooltip("Element pool row id (same id merges across dice).")]
    [SerializeField] private string poolRowId = "Cleanse";

    public int CleanseStacks => stacks;

    public PoolRowKey GetPoolRowKey()
    {
        var row = string.IsNullOrWhiteSpace(poolRowId) ? "Cleanse" : poolRowId.Trim();
        return PoolRowKey.FromInspectorString(row);
    }

    protected override ActionVisualId VisualKey => ActionVisualId.Cleanse;

    /// <summary>Filled when the face resolves so flyouts / element bar show pending cleanse for the turn.</summary>
    public void AppendPoolContributionIfAny(FaceResult result)
    {
        if (result == null || stacks <= 0)
            return;

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = GetPoolRowKey(),
            Amount = stacks,
            Icon = ResolveActionIcon(),
            PoolRowBackground = GameIconCatalog.GetActionBackground(GetActionVisualId()),
            PerfectStrikeScales = true
        });
    }

    public override void Execute(GameActionContext context)
    {
        if (context == null || context.CombatManager == null)
            return;

        var cleanseStacks = stacks;
        var fromEnemyAction = context.SourceEnemyAction != null;

        if (fromEnemyAction)
        {
            var finalStacks = cleanseStacks;
            var statusCtx = new StatusEffectContext
            {
                CombatManager = context.CombatManager,
                Player = context.Player,
                Enemy = context.Enemy
            };

            var targetStatusManager = context.Enemy?.StatusEffects;
            var ownerTarget = StatusEffectTarget.Enemy;
            var reduced = targetStatusManager != null &&
                          targetStatusManager.ReduceRandomDebuffStacksForTarget(finalStacks, statusCtx, ownerTarget);

            if (GameActionDebug.Enabled)
                Debug.Log(reduced
                    ? $"[Cleanse] Enemy intent reduced a random debuff by {finalStacks} stack(s)."
                    : "[Cleanse] No debuffs to cleanse");
            return;
        }

        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var finalStacks = ctx.CombatManager.ResolveCleansePoolGrant(this);
            if (finalStacks <= 0)
                return;

            var statusCtx = new StatusEffectContext
            {
                CombatManager = ctx.CombatManager,
                Player = ctx.Player,
                Enemy = ctx.Enemy
            };

            var targetStatusManager = ctx.Player?.StatusEffects;
            var reduced = targetStatusManager != null &&
                          targetStatusManager.ReduceRandomDebuffStacksForTarget(finalStacks, statusCtx, StatusEffectTarget.Player);

            if (GameActionDebug.Enabled)
                Debug.Log(reduced
                    ? $"[Cleanse] Reduced a random debuff by {finalStacks} stack(s) (base: {cleanseStacks}, multiplier: {ctx.CombatManager.GetAppliedMultiplier()})"
                    : "[Cleanse] No debuffs to cleanse");
        });
    }
}
