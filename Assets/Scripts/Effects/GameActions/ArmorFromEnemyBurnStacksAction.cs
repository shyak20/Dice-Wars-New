using System;
using UnityEngine;

/// <summary>
/// Adds pending armor equal to (total <see cref="BurnEffectSO"/> stacks on all living enemies) × (percent / 100), floored.
/// </summary>
[Serializable]
public class ArmorFromEnemyBurnStacksAction : GameActionWithIcon
{
    [Tooltip("Each 100 = armor equal to 100% of enemy burn stacks (e.g. 50 → half stacks, rounded down).")]
    [SerializeField, Min(0)]
    private int armorPercentOfBurnStacks = 100;

    public int ArmorPercentOfBurnStacks => armorPercentOfBurnStacks;

    protected override ActionVisualId VisualKey => ActionVisualId.ArmorFromEnemyBurnStacks;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || armorPercentOfBurnStacks <= 0)
            return;

        var stacks = SumAllEnemyBurnStacks(context.CombatManager);
        if (stacks <= 0)
            return;

        var armor = stacks * armorPercentOfBurnStacks / 100;
        if (armor <= 0)
            return;

        context.CombatManager.AddBonusArmorFromAction(armor);

        if (GameActionDebug.Enabled)
            Debug.Log($"[ArmorFromEnemyBurnStacks] +{armor} bonus armor ({armorPercentOfBurnStacks}% of {stacks} burn stacks across all enemies).");
    }

    static int SumAllEnemyBurnStacks(CombatManager combat)
    {
        var enemies = combat.ActiveEnemies;
        if (enemies == null)
            return 0;

        var sum = 0;
        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
                continue;
            sum += SumEnemyBurnStacks(enemy);
        }

        return sum;
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
