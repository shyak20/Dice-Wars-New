using System;
using UnityEngine;

[Serializable]
public class HealAction : GameActionWithIcon
{
    [SerializeField] private int amount = 1;
    [Tooltip("Element pool row id (same id merges across dice).")]
    [SerializeField] private string poolRowId = "Heal";

    public int Amount => amount;

    public PoolRowKey GetPoolRowKey()
    {
        var row = string.IsNullOrWhiteSpace(poolRowId) ? "Heal" : poolRowId.Trim();
        return PoolRowKey.FromInspectorString(row);
    }

    protected override ActionVisualId VisualKey => ActionVisualId.Heal;

    /// <summary>Filled when the face resolves so flyouts / element bar show pending heal for the turn.</summary>
    public void AppendPoolContributionIfAny(FaceResult result)
    {
        if (result == null || amount <= 0)
            return;
        var row = string.IsNullOrWhiteSpace(poolRowId) ? "Heal" : poolRowId.Trim();
        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = PoolRowKey.FromInspectorString(row),
            Amount = amount,
            Icon = ResolveActionIcon(),
            PoolRowBackground = GameIconCatalog.GetActionBackground(GetActionVisualId()),
            PerfectStrikeScales = true
        });
    }

    public override void Execute(GameActionContext context)
    {
        if (context == null || context.CombatManager == null)
            return;

        var fromEnemyAction = context.SourceEnemyAction != null;
        if (fromEnemyAction)
        {
            if (GameActionDebug.Enabled)
                Debug.Log($"[HealAction] Enemy intent heals {amount} HP.");
            context.Enemy?.Heal(amount);
            return;
        }

        // Resolve after Perfect Cast / pool edits (same pattern as Cleanse/Thorns) so grant matches the UI stacks.
        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var finalHeal = ctx.CombatManager.ResolveHealPoolGrant(this);
            if (finalHeal <= 0)
                return;

            if (GameActionDebug.Enabled)
                Debug.Log(
                    $"[HealAction] Healing {finalHeal} HP (configured: {amount}, multiplier: {ctx.CombatManager.GetAppliedMultiplier()})");
            ctx.Player.Heal(finalHeal);
        });
    }
}
