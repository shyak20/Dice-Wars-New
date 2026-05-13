using System;
using UnityEngine;

[Serializable]
public class HealAction : GameActionWithIcon
{
    [SerializeField] private int amount = 1;
    [Tooltip("Element pool row id (same id merges across dice).")]
    [SerializeField] private string poolRowId = "Heal";

    public int Amount => amount;

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
            PoolRowBackground = GameIconCatalog.GetActionBackground(GetActionVisualId())
        });
    }

    public override void Execute(GameActionContext context)
    {
        if (context == null || context.CombatManager == null)
            return;

        var healAmount = amount;
        var fromEnemyAction = context.SourceEnemyAction != null;
        if (fromEnemyAction)
        {
            if (GameActionDebug.Enabled)
                Debug.Log($"[HealAction] Enemy intent heals {healAmount} HP.");
            context.Enemy?.Heal(healAmount);
            return;
        }

        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var finalHeal = healAmount * ctx.CombatManager.GetAppliedMultiplier();
            if (GameActionDebug.Enabled)
                Debug.Log($"[HealAction] Healing {finalHeal} HP (base: {healAmount}, multiplier: {ctx.CombatManager.GetAppliedMultiplier()})");
            ctx.Player.Heal(finalHeal);
        });
    }
}
