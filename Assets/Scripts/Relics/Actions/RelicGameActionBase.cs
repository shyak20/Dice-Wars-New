using System;
using UnityEngine;

/// <summary>
/// Serializable relic-only <see cref="IGameAction"/> entries for <see cref="RelicSO.actions"/>.
/// Keeps the Unity <c>SerializeReference</c> picker separate from die-face actions.
/// </summary>
[Serializable]
public abstract class RelicGameActionBase : IGameAction
{
    [SerializeField, Tooltip("Shown for consistency with die-face actions. Relic effects still run when Execute handles the current RelicPhase.")]
    private bool activateImmediately = true;

    /// <inheritdoc />
    public bool ActivateImmediately => activateImmediately;

    public abstract void Execute(GameActionContext ctx);
}
