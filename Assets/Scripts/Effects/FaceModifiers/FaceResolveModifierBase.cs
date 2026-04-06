using UnityEngine;

/// <summary>
/// Runs during <see cref="CombatManager.ResolveRollResult"/> before <see cref="TurnRegistry.RecordResolvedFace"/>
/// so damage math can use turn memory (accumulated armor/physical). <see cref="Execute"/> is a no-op;
/// place these in the same <see cref="DieFaceSO.actions"/> list as <see cref="IGameAction"/> entries.
/// </summary>
public abstract class FaceResolveModifierBase : IGameAction
{
    public void Execute(GameActionContext context)
    {
        // Modifiers apply in ResolveRollResult; optional overrides for rare dual-phase actions.
    }

    public abstract void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry);
}
