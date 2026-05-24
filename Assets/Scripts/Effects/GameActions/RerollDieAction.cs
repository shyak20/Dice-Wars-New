using System;
using UnityEngine;

/// <summary>
/// Marker: grants a reroll during the batch special-effects phase.
/// <see cref="RerollDieScope.RerollTriggeringDieOnly"/> rethrows the die that rolled this face;
/// <see cref="RerollDieScope.PlayerChoosesAnyDie"/> opens the picker (legacy default).
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

    public RerollDieScope Scope => scope;

    public bool KeepSameFaceOnReroll => keepSameFaceOnReroll;

    protected override ActionVisualId VisualKey => ActionVisualId.RerollDie;

    public override void Execute(GameActionContext context)
    {
    }
}
