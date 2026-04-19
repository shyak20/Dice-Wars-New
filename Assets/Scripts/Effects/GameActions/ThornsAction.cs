using System;
using UnityEngine;

[Serializable]
public class ThornsAction : GameActionWithIcon
{
    [SerializeField] private int amount = 1;

    [Tooltip("Thorns status asset: set Target to Player or Enemy for who receives the stacks.")]
    [SerializeField] private ThornsEffectSO thornsDefinition;

    [Tooltip("Optional. Pool row id (defaults to the Thorns asset name). Same id merges across dice.")]
    [SerializeField] private string poolRowKeyOverride;

    public int ThornsPerHit => amount;

    public ThornsEffectSO ThornsDefinition => thornsDefinition;

    protected override ActionVisualId VisualKey => ActionVisualId.Thorns;

    PoolRowKey ResolvePoolRowKey()
    {
        if (!string.IsNullOrWhiteSpace(poolRowKeyOverride))
            return PoolRowKey.FromInspectorString(poolRowKeyOverride);
        if (thornsDefinition != null)
            return PoolRowKey.Custom(thornsDefinition.name);
        return PoolRowKey.Custom("Thorns");
    }

    public void AppendPoolContributionIfAny(FaceResult result, bool activateImmediately)
    {
        if (activateImmediately) return;
        if (result == null || amount <= 0 || thornsDefinition == null) return;

        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = ResolvePoolRowKey(),
            Amount = amount,
            Icon = ResolveActionIcon()
        });
    }

    public override void Execute(GameActionContext context)
    {
        if (thornsDefinition == null)
        {
            Debug.LogError("[ThornsAction] Assign thornsDefinition (Thorns status asset with correct Player/Enemy target).");
            return;
        }

        if (context?.CombatManager == null || context.Player == null || context.Enemy == null)
        {
            Debug.LogError("[ThornsAction] Missing CombatManager / Player / Enemy on context.");
            return;
        }

        var manager = thornsDefinition.target == StatusEffectTarget.Player
            ? context.Player.StatusEffects
            : context.Enemy.StatusEffects;

        manager.ApplyStatus(thornsDefinition, amount, context.CombatManager.BuildStatusContextForEffects());

        if (GameActionDebug.Enabled)
            Debug.Log($"[ThornsAction] Applied {amount} Thorns stack(s) to {thornsDefinition.target}");
    }
}
