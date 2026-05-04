using System;
using UnityEngine;

/// <summary>
/// Immediately deals damage to the enemy equal to (total <see cref="BurnEffectSO"/> stacks) × (percent / 100), floored.
/// Uses <see cref="EnemyDamagePresentationKind.Burn"/> (armor applies like other damage). Does not remove or change burn stacks.
/// </summary>
[Serializable]
public class InstantBurnDamageFromEnemyStacksPercentAction : GameActionWithIcon
{
    [Tooltip("Each 100 = deal damage equal to 100% of current total burn stacks (e.g. 25 → quarter of stacks, rounded down).")]
    [SerializeField, Min(0)]
    private int dealPercentOfBurnStacksAsDamage = 25;

    public int DealPercentOfBurnStacksAsDamage => dealPercentOfBurnStacksAsDamage;

    protected override ActionVisualId VisualKey => ActionVisualId.InstantBurnProcFromStacks;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || dealPercentOfBurnStacksAsDamage <= 0)
            return;

        var enemy = context.Enemy;
        if (enemy == null)
        {
            Debug.LogError("InstantBurnDamageFromEnemyStacksPercentAction: no enemy on context.");
            return;
        }

        var stacks = SumEnemyBurnStacks(enemy);
        if (stacks <= 0)
            return;

        var damage = stacks * dealPercentOfBurnStacksAsDamage / 100;
        if (damage <= 0)
            return;

        enemy.TakeDamage(damage, EnemyDamagePresentationKind.Burn);
        context.CombatManager.TryResolveVictoryAfterDirectEnemyDamage();

        if (GameActionDebug.Enabled)
            Debug.Log($"[InstantBurnDamageFromStacks] {damage} burn damage ({dealPercentOfBurnStacksAsDamage}% of {stacks} stacks, stacks unchanged).");
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
