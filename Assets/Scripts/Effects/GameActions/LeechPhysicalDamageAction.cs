using System;
using UnityEngine;

/// <summary>
/// Enemy intent game action: physical hit(s) on the player (Strength, Chill, Immune, Thorns, …)
/// that heal the acting enemy for unblocked HP damage dealt per hit.
/// </summary>
[Serializable]
public class LeechPhysicalDamageAction : GameActionWithIcon
{
    [SerializeField, Min(0)] private int damage = 1;
    [SerializeField, Min(1)] private int numberOfAttacks = 1;

    public int Damage => damage;
    public int NumberOfAttacks => numberOfAttacks;

    protected override ActionVisualId VisualKey => ActionVisualId.LeechPhysicalDamage;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || damage <= 0)
            return;

        var enemy = context.Enemy;
        if (enemy == null)
        {
            Debug.LogError("LeechPhysicalDamageAction: no enemy on context.");
            return;
        }

        context.CombatManager.ApplyEnemyPhysicalLeechHit(damage, enemy);

        if (GameActionDebug.Enabled)
            Debug.Log($"[LeechPhysicalDamage] {enemy.name} leech hit for base {damage}.");
    }
}
