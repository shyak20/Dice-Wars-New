using System;
using UnityEngine;

/// <summary>
/// Adds pending attack equal to (total enemy <see cref="BurnEffectSO"/> stacks) × (percent / 100), floored.
/// Counts every burn instance on the enemy (multiple burn assets sum together).
/// </summary>
[Serializable]
public class DamageFromEnemyBurnStacksPercentAction : GameActionWithIcon
{
    [Tooltip("Each 100 = 100% of current burn stacks as damage (e.g. 50 → half stacks, rounded down).")]
    [SerializeField, Min(0)]
    private int damagePercentOfBurnStacks = 50;

    public int DamagePercentOfBurnStacks => damagePercentOfBurnStacks;

    protected override ActionVisualId VisualKey => ActionVisualId.DamageFromEnemyBurnStacks;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || damagePercentOfBurnStacks <= 0)
            return;

        var enemy = context.Enemy;
        if (enemy == null)
        {
            Debug.LogError("DamageFromEnemyBurnStacksPercentAction: no enemy on context.");
            return;
        }

        var stacks = SumEnemyBurnStacks(enemy);
        if (stacks <= 0)
            return;

        var bonus = stacks * damagePercentOfBurnStacks / 100;
        if (bonus <= 0)
            return;

        context.CombatManager.AddBonusDamageFromAction(bonus);

        if (GameActionDebug.Enabled)
            Debug.Log($"[DamageFromEnemyBurnStacksPercent] +{bonus} damage ({damagePercentOfBurnStacks}% of {stacks} burn stacks).");
    }

    static int SumEnemyBurnStacks(EnemyController enemy)
    {
        var mgr = enemy.StatusEffects;
        if (mgr == null) return 0;
        var effects = mgr.Effects;
        if (effects == null) return 0;
        var sum = 0;
        for (var i = 0; i < effects.Count; i++)
        {
            if (effects[i]?.Definition is BurnEffectSO)
                sum += effects[i].Stacks;
        }

        return sum;
    }
}
