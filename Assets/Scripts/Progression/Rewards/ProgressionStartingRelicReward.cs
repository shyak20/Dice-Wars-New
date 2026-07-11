using System;
using UnityEngine;

/// <summary>
/// Meta reward: grants <see cref="relic"/> as a starting relic on the character
/// (<see cref="PlayerDataSO.startingRelics"/>). Equipped at run start via
/// <see cref="ProgressionManager.GetStartingRelicsForNewRun"/>.
/// </summary>
[Serializable]
public sealed class ProgressionStartingRelicReward : ProgressionRewardBase
{
    public RelicSO relic;

    public override void Apply(ProgressionRewardApplyContext context)
    {
        if (relic == null)
            return;

        if (context?.CharacterTemplate != null)
            AddUnique(context.CharacterTemplate, relic);

        var container = PlayerDataContainer.Instance;
        if (container?.RuntimeData != null
            && container.ActiveCharacterTemplate == context?.CharacterTemplate)
        {
            AddUnique(container.RuntimeData, relic);
        }
    }

    static void AddUnique(PlayerDataSO profile, RelicSO relicToAdd)
    {
        if (profile == null || relicToAdd == null)
            return;

        profile.startingRelics ??= new System.Collections.Generic.List<RelicSO>();
        if (profile.startingRelics.Contains(relicToAdd))
            return;

        profile.startingRelics.Add(relicToAdd);
    }
}
