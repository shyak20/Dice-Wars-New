using System;
using UnityEngine;

[Serializable]
public sealed class ProgressionMaxHpReward : ProgressionRewardBase
{
    [Min(1)] public int amount = 5;

    public override void Apply(ProgressionRewardApplyContext context) { }
}

[Serializable]
public sealed class ProgressionMaxPowerReward : ProgressionRewardBase
{
    [Min(1)] public int amount = 1;

    public override void Apply(ProgressionRewardApplyContext context) { }
}

[Serializable]
public sealed class ProgressionStartingGoldReward : ProgressionRewardBase
{
    [Min(0)] public int amount = 10;

    public override void Apply(ProgressionRewardApplyContext context) { }
}

[Serializable]
public sealed class ProgressionMapMoveLimitReward : ProgressionRewardBase
{
    [Min(1)] public int amount = 1;

    public override void Apply(ProgressionRewardApplyContext context) { }
}

[Serializable]
public sealed class ProgressionMaxRollsReward : ProgressionRewardBase
{
    [Min(1)] public int amount = 1;

    public override void Apply(ProgressionRewardApplyContext context) { }
}
