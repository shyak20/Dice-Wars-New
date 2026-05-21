using System;
using UnityEngine;

/// <summary>
/// Polymorphic meta reward (rank-up list or trial completion). Use + in the inspector to add a typed reward.
/// </summary>
[Serializable]
public abstract class ProgressionRewardBase
{
    public abstract void Apply(ProgressionRewardApplyContext context);
}
