using System;
using UnityEngine;

/// <summary>
/// "Meditate"-style: grants +X max HP at turn end (scaled by Perfect Strike like other turn-end actions) and heals for the same amount.
/// Adds a Nature pool row when the die resolves; grant matches pool totals after jackpot scaling.
/// </summary>
[Serializable]
public class MaxHpAction : GameActionWithIcon
{
    [SerializeField] private int amount = 1;

    [Tooltip("Which element bar bucket shows this pending +max HP until the turn is submitted.")]
    [SerializeField] private DieType poolDisplayAs = DieType.Nature;

    public int Amount => amount;

    protected override ActionVisualId VisualKey => ActionVisualId.MaxHp;

    public override void Execute(GameActionContext context)
    {
        if (GameActionDebug.Enabled)
            Debug.Log($"[MaxHpAction] Queue +{amount} max HP (with heal)");

        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var finalAmount = ctx.CombatManager.ResolveMaxHpPoolGrant(this);
            if (finalAmount <= 0) return;
            ctx.Player.AddMaxHealthAndHeal(finalAmount);
        });
    }

    /// <summary>Filled when the face resolves so flyouts / element bar show pending +max HP for the turn.</summary>
    public void AppendPoolContributionIfAny(FaceResult result)
    {
        if (result == null || amount <= 0) return;
        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolType = poolDisplayAs,
            Amount = amount,
            Icon = ResolveActionIcon(),
            MaxHpPoolSource = this
        });
    }
}
