using System;
using UnityEngine;

[Serializable]
public class AddRollsAction : GameActionWithIcon
{
    [SerializeField] private int amount = 1;

    protected override ActionVisualId VisualKey => ActionVisualId.AddRolls;

    public override void Execute(GameActionContext context)
    {
        if (context.CombatManager == null) return;

        var delta = Mathf.Max(0, amount);
        context.CombatManager.AddRollsRemaining(delta);

        if (GameActionDebug.Enabled)
            Debug.Log($"[AddRollsAction] Added +{delta} roll(s) this turn");
    }

    /// <summary>Immediate faces: flyout + pool row for UI. Deferred faces: skip (rolls apply when actions run at turn end).</summary>
    public void AppendPoolContributionIfAny(FaceResult result, bool activateImmediately)
    {
        if (result == null || !activateImmediately) return;
        var delta = Mathf.Max(0, amount);
        if (delta <= 0) return;

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = PoolRowKey.Custom(CombatPoolRowIds.ExtraRolls),
            Amount = delta,
            Icon = GameIconCatalog.GetActionIcon(ActionVisualId.AddRolls),
            VisualFlyoutOnly = true
        });
    }
}
