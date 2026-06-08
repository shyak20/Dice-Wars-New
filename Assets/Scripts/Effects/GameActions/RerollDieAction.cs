using System;
using UnityEngine;

/// <summary>
/// Marker: grants a reroll after this face's damage, armor, and other actions are submitted to pools / enemies.
/// <see cref="RerollDieScope.RerollTriggeringDieOnly"/> rethrows the die that rolled this face (instead of dissolving it);
/// <see cref="RerollDieScope.PlayerChoosesAnyDie"/> opens the picker before gather (legacy default).
/// </summary>
[Serializable]
public class RerollDieAction : GameActionWithIcon
{
    public enum RerollDieScope
    {
        PlayerChoosesAnyDie,
        RerollTriggeringDieOnly,
    }

    [SerializeField] private RerollDieScope scope = RerollDieScope.PlayerChoosesAnyDie;

    [Tooltip("When on with RerollTriggeringDieOnly, physics reroll runs but the same face is committed (Roll Again).")]
    [SerializeField] private bool keepSameFaceOnReroll;

    [Tooltip("When on with RerollTriggeringDieOnly, skip the reroll if power already qualifies for Perfect Cast after this face is submitted.")]
    [SerializeField] private bool skipRerollWhenPerfectCast;

    public RerollDieScope Scope => scope;

    public bool KeepSameFaceOnReroll => keepSameFaceOnReroll;

    public bool SkipRerollWhenPerfectCast => skipRerollWhenPerfectCast;

    protected override ActionVisualId VisualKey => ActionVisualId.RerollDie;

    public override void Execute(GameActionContext context)
    {
    }
}
