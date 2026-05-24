using System;
using UnityEngine;

/// <summary>
/// Polymorphic meta reward (rank-up list or trial completion). Use + in the inspector to add a typed reward.
/// </summary>
[Serializable]
public abstract class ProgressionRewardBase
{
    [Tooltip("Optional trial-reward row text. {0} = numeric amount or relic name. Empty uses type default.")]
    public string rowFormat;

    public abstract void Apply(ProgressionRewardApplyContext context);
}
