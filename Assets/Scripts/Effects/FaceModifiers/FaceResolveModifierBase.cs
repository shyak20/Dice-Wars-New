using System;
using UnityEngine;

/// <summary>
/// Runs during combat gather (after special-effects phase) before <see cref="TurnRegistry.RecordResolvedFace"/>.
/// <see cref="Execute"/> is a no-op; place these in the same <see cref="DieFaceSO.actions"/> list as other <see cref="IGameAction"/> entries.
/// When <see cref="ActivateImmediately"/> is on, <see cref="Modify"/> runs before relics and gems; when off, after value-based roll watcher armor bonuses (still before this face is recorded).
/// </summary>
[Serializable]
public abstract class FaceResolveModifierBase : IGameAction
{
    [SerializeField, Tooltip("When on, Modify runs before relics and gems. When off, Modify runs after roll-watcher armor changes and before this face is recorded.")]
    private bool activateImmediately = true;

    /// <inheritdoc />
    public bool ActivateImmediately => activateImmediately;

    public void Execute(GameActionContext context)
    {
        // Modifiers apply during combat gather (CommitResolvedRoll); optional overrides for rare dual-phase actions.
    }

    public abstract void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry);
}
