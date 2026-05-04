using System;
using UnityEngine;

/// <summary>
/// Adds pending attack: <see cref="baseDamage"/>, or <see cref="baseDamage"/> × <see cref="damageMultiplierIfMet"/> when total enemy <see cref="BurnEffectSO"/> stacks ≥ <see cref="burnStackThreshold"/>.
/// </summary>
[Serializable]
public class BonusDamageIfEnemyBurnMeetsThresholdAction : GameActionWithIcon
{
    [SerializeField, Min(0)]
    private int baseDamage = 4;

    [Tooltip("Total enemy burn stacks (all burn instances summed) must be at least this for the multiplier to apply.")]
    [SerializeField, Min(0)]
    private int burnStackThreshold = 3;

    [Tooltip("When the threshold is met, pending damage = baseDamage × this value (e.g. 2 = double).")]
    [SerializeField, Min(1)]
    private int damageMultiplierIfMet = 2;

    public int BaseDamage => baseDamage;
    public int BurnStackThreshold => burnStackThreshold;
    public int DamageMultiplierIfMet => damageMultiplierIfMet;

    protected override ActionVisualId VisualKey => ActionVisualId.BonusDamageVsBurnThreshold;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || baseDamage <= 0)
            return;

        var enemy = context.Enemy;
        if (enemy == null)
        {
            Debug.LogError("BonusDamageIfEnemyBurnMeetsThresholdAction: no enemy on context.");
            return;
        }

        var stacks = SumEnemyBurnStacks(enemy);
        var damage = baseDamage;
        if (stacks >= burnStackThreshold && damageMultiplierIfMet > 1)
            damage = baseDamage * damageMultiplierIfMet;

        if (damage <= 0)
            return;

        context.CombatManager.AddBonusDamageFromAction(damage);

        if (GameActionDebug.Enabled)
            Debug.Log($"[BonusDamageVsBurnThreshold] +{damage} pending damage (base {baseDamage}, enemy burn {stacks}, threshold {burnStackThreshold}, mult {damageMultiplierIfMet}).");
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
