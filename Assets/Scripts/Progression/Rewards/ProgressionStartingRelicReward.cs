using System;
using UnityEngine;

/// <summary>
/// Meta reward: player begins each run with <see cref="relic"/> already equipped
/// (see <see cref="ProgressionRunModifiers.CollectStartingRelics"/>).
/// </summary>
[Serializable]
public sealed class ProgressionStartingRelicReward : ProgressionRewardBase
{
    public RelicSO relic;

    public override void Apply(ProgressionRewardApplyContext context) { }
}
