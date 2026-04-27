using System;
using UnityEngine;

/// <summary>
/// Adds bonus pending attack equal to (owned dice count of a type) × amount.
/// Default setup matches: "Deals extra 6 damage for every Physical die you have."
/// </summary>
[Serializable]
public class BonusDamagePerOwnedDieTypeAction : GameActionWithIcon
{
    [Tooltip("Which owned die type to count. Physical uses DieType.Damage.")]
    [SerializeField] private DieType countedDieType = DieType.Damage;

    [Tooltip("Bonus damage added per owned die of the selected type.")]
    [SerializeField, Min(0)] private int damagePerOwnedDie = 6;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || damagePerOwnedDie <= 0)
            return;

        var runtimeDeck = PlayerDataContainer.Instance?.RuntimeData?.currentDeck;
        if (runtimeDeck == null)
        {
            Debug.LogError("BonusDamagePerOwnedDieTypeAction: player runtime deck is unavailable.");
            return;
        }

        var ownedCount = 0;
        for (var i = 0; i < runtimeDeck.Count; i++)
        {
            var die = runtimeDeck[i];
            if (die != null && die.dieType == countedDieType)
                ownedCount++;
        }

        if (ownedCount <= 0)
            return;

        var bonus = ownedCount * damagePerOwnedDie;
        context.CombatManager.AddBonusDamageFromAction(bonus);

        if (GameActionDebug.Enabled)
            Debug.Log($"[BonusDamagePerOwnedDieTypeAction] +{bonus} damage ({ownedCount} x {damagePerOwnedDie} for {countedDieType}).");
    }
}
