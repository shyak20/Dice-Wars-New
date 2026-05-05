using UnityEngine;

/// <summary>
/// Computes the combat max-power budget from deck, shrine bonuses, and relic queries —
/// same rules as <see cref="CombatManager"/> without needing an active combat scene.
/// </summary>
public static class PlayerMaxPowerForRun
{
    public static int Compute(PlayerDataSO playerData)
    {
        if (playerData == null)
            return 12;

        var maxPower = playerData.baseMaxPower;
        if (RunManager.Instance != null)
            maxPower += RunManager.Instance.RunShrineBonusMaxPower;

        if (playerData.currentDeck != null)
        {
            for (var i = 2; i < playerData.currentDeck.Count; i++)
            {
                var die = playerData.currentDeck[i];
                if (die != null)
                    maxPower += die.MaxPowerContribution;
            }
        }

        maxPower += RelicActionRunner.QueryMaxPowerBonusFromRelics(playerData);
        return maxPower;
    }
}
