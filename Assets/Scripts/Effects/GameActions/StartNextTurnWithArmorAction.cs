using System;
using UnityEngine;

/// <summary>
/// Next player turn starts with X armor instead of resetting to 0.
/// Deferred (default): element pool row + <see cref="CombatManager.SchedulePlayerArmorAtNextTurnStart"/> at turn submit.
/// Activate Immediately: applies <see cref="NextTurnArmorEffectSO"/> on the player status bar (stacks = armor; Perfect Strike scales stacks).
/// </summary>
[Serializable]
public class StartNextTurnWithArmorAction : GameActionWithIcon
{
    [SerializeField, Min(0)] private int armorAmount = 1;
    [Tooltip("Element pool row id when deferred (same id merges across dice).")]
    [SerializeField] private string poolRowId = "Armor";
    [Tooltip("Required when Activate Immediately is on — shown on the player status bar until next turn.")]
    [SerializeField] private NextTurnArmorEffectSO nextTurnArmorStatus;

    public int ArmorAmount => armorAmount;

    protected override ActionVisualId VisualKey => ActionVisualId.StartNextTurnWithArmor;

    public void AppendPoolContributionIfAny(FaceResult result)
    {
        if (ActivateImmediately || result == null || armorAmount <= 0)
            return;

        var row = string.IsNullOrWhiteSpace(poolRowId) ? "Armor" : poolRowId.Trim();
        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = PoolRowKey.FromInspectorString(row),
            Amount = armorAmount,
            Icon = ResolveActionIcon(),
            PoolRowBackground = GameIconCatalog.GetActionBackground(GetActionVisualId()),
            PerfectStrikeScales = true
        });
    }

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || armorAmount <= 0)
            return;

        var baseAmount = armorAmount;

        if (ActivateImmediately)
        {
            if (nextTurnArmorStatus == null)
            {
                Debug.LogError(
                    "StartNextTurnWithArmorAction: assign nextTurnArmorStatus when Activate Immediately is enabled.");
                return;
            }

            if (context.Player?.StatusEffects == null)
            {
                Debug.LogError("StartNextTurnWithArmorAction: player StatusEffectManager is missing.");
                return;
            }

            var statusCtx = new StatusEffectContext
            {
                CombatManager = context.CombatManager,
                Player = context.Player,
                Enemy = context.Enemy
            };
            context.Player.StatusEffects.ApplyStatus(nextTurnArmorStatus, baseAmount, statusCtx);

            if (GameActionDebug.Enabled)
                Debug.Log($"[StartNextTurnWithArmor] Applied {baseAmount} pending armor via status (immediate).");
            return;
        }

        context.CombatManager.QueueTurnEndAction(ctx =>
        {
            var final = baseAmount * ctx.CombatManager.GetAppliedMultiplier();
            if (final <= 0)
                return;

            ctx.CombatManager.SchedulePlayerArmorAtNextTurnStart(final);
            if (GameActionDebug.Enabled)
                Debug.Log(
                    $"[StartNextTurnWithArmor] Scheduled {final} armor at next turn start (base {baseAmount}, ×{ctx.CombatManager.GetAppliedMultiplier()}).");
        });
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (ActivateImmediately && nextTurnArmorStatus == null)
            Debug.LogWarning(
                $"{nameof(StartNextTurnWithArmorAction)}: assign {nameof(nextTurnArmorStatus)} for immediate / status-bar mode.");
    }
#endif
}
