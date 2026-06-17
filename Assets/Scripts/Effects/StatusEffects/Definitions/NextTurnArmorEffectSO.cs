using UnityEngine;

/// <summary>
/// Stacks = armor granted at the owner's next <see cref="StatusEffectSO.OnTurnStart"/> (then consumed).
/// Used by <see cref="StartNextTurnWithArmorAction"/> when Activate Immediately is on.
/// </summary>
[CreateAssetMenu(fileName = "NextTurnArmor", menuName = "DiceGame/StatusEffects/Next Turn Armor")]
public class NextTurnArmorEffectSO : StatusEffectSO
{
    public override void OnTurnStart(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        if (ctx.Player == null || instance.Stacks <= 0)
            return;

        var armor = instance.Stacks;
        ctx.Player.SetArmor(armor);
        instance.RemoveStacks(armor);

        if (GameActionDebug.Enabled)
            Debug.Log($"[NextTurnArmor] Player turn started with {armor} armor (status consumed).");
    }

    public override void OnPerfectStrike(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        if (ctx.CombatManager == null || instance.Stacks <= 0)
            return;

        var mult = ctx.CombatManager.GetAppliedMultiplier();
        if (mult <= 1)
            return;

        var bonus = instance.Stacks * (mult - 1);
        if (bonus <= 0)
            return;

        instance.AddStacks(bonus);
        if (GameActionDebug.Enabled)
            Debug.Log($"[NextTurnArmor] Perfect Strike scaled pending armor to {instance.Stacks} (×{mult}).");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        stackDecayPerTurn = 0;
        target = StatusEffectTarget.Player;
        type = StatusEffectType.Buff;
    }
#endif
}
