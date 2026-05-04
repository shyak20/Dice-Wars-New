using System;
using UnityEngine;

/// <summary>
/// Removes all <see cref="BurnEffectSO"/> on the chosen target, then the player gains 1 max HP (and heal) per <see cref="stacksPerMaxHp"/> stacks removed (floored).
/// </summary>
[Serializable]
public class ConsumeAllBurnForMaxHpAction : GameActionWithIcon
{
    [Tooltip("Who loses all Burn stacks (player still receives +max HP).")]
    [SerializeField] private StatusEffectTarget burnTarget = StatusEffectTarget.Enemy;

    [Tooltip("Stacks removed ÷ this value = max HP gained (integer division). Example: 3 → one +max HP per 3 stacks removed.")]
    [SerializeField, Min(1)]
    private int stacksPerMaxHp = 1;

    public StatusEffectTarget BurnTarget => burnTarget;
    public int StacksPerMaxHp => stacksPerMaxHp;

    protected override ActionVisualId VisualKey => ActionVisualId.ConsumeBurnForMaxHp;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || stacksPerMaxHp <= 0)
            return;

        var player = context.Player;
        if (player == null)
        {
            Debug.LogError("ConsumeAllBurnForMaxHpAction: no player on context.");
            return;
        }

        var holder = burnTarget == StatusEffectTarget.Player ? player.StatusEffects : context.Enemy?.StatusEffects;
        if (holder == null)
        {
            Debug.LogError("ConsumeAllBurnForMaxHpAction: no status manager for burn target.");
            return;
        }

        var statusCtx = context.CombatManager.BuildStatusContextForEffects();
        var removed = holder.RemoveAllBurnStacks(statusCtx);
        if (removed <= 0)
            return;

        var maxHpGain = removed / stacksPerMaxHp;
        if (maxHpGain <= 0)
            return;

        player.AddMaxHealthAndHeal(maxHpGain);

        if (GameActionDebug.Enabled)
            Debug.Log($"[ConsumeAllBurnForMaxHp] Removed {removed} burn stack(s) on {burnTarget}; +{maxHpGain} max HP (1 per {stacksPerMaxHp} stacks).");
    }
}
