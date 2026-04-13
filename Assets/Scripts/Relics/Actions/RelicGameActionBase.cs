using System;

/// <summary>
/// Serializable relic-only <see cref="IGameAction"/> entries for <see cref="RelicSO.actions"/>.
/// Keeps the Unity <c>SerializeReference</c> picker separate from die-face actions.
/// </summary>
[Serializable]
public abstract class RelicGameActionBase : IGameAction
{
    public abstract void Execute(GameActionContext ctx);
}
