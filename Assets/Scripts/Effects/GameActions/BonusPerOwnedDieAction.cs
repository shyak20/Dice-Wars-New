using System;
using UnityEngine;

/// <summary>
/// Adds a bonus scaled by dice in the player's deck: (matching die count) × <see cref="amountPerOwnedDie"/>.
/// Supports armor, pending damage, fire (burn), and poison (via assigned status assets).
/// </summary>
[Serializable]
public class BonusPerOwnedDieAction : GameActionWithIcon
{
    [SerializeField] private CombatBonusChannel bonusChannel = CombatBonusChannel.Armor;

    [Tooltip("Bonus magnitude per counted die in the deck.")]
    [SerializeField, Min(0)] private int amountPerOwnedDie = 1;

    [Tooltip("When off, every non-null die in the deck counts. When on, only dice matching Counted Die Type count.")]
    [SerializeField] private bool countOnlySpecificDieType;

    [Tooltip("Used when Count Only Specific Die Type is enabled (Physical = DieType.Damage).")]
    [SerializeField] private DieType countedDieType = DieType.Damage;

    [Tooltip("Required when Bonus Channel is Fire.")]
    [SerializeField] private BurnEffectSO burnDefinition;

    [Tooltip("Required when Bonus Channel is Poison.")]
    [SerializeField] private PoisonEffectSO poisonDefinition;

    public CombatBonusChannel BonusChannel => bonusChannel;
    public int AmountPerOwnedDie => amountPerOwnedDie;

    protected override ActionVisualId VisualKey => ActionVisualId.BonusPerOwnedDie;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null || amountPerOwnedDie <= 0)
            return;

        var ownedCount = CountOwnedDice();
        if (ownedCount <= 0)
            return;

        var total = ownedCount * amountPerOwnedDie;
        CombatBonusChannelApplicator.ApplyDeferredTurnBonus(
            bonusChannel,
            total,
            context,
            burnDefinition,
            poisonDefinition);

        if (GameActionDebug.Enabled)
            Debug.Log($"[BonusPerOwnedDieAction] +{total} {bonusChannel} ({ownedCount} × {amountPerOwnedDie}).");
    }

    int CountOwnedDice()
    {
        var runtimeDeck = PlayerDataContainer.Instance?.RuntimeData?.currentDeck;
        if (runtimeDeck == null)
        {
            Debug.LogError("BonusPerOwnedDieAction: player runtime deck is unavailable.");
            return 0;
        }

        var ownedCount = 0;
        for (var i = 0; i < runtimeDeck.Count; i++)
        {
            var die = runtimeDeck[i];
            if (die == null)
                continue;
            if (countOnlySpecificDieType && die.dieType != countedDieType)
                continue;
            ownedCount++;
        }

        return ownedCount;
    }
}
