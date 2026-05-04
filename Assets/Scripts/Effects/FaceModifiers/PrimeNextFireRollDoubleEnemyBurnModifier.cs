using System;
using UnityEngine;

/// <summary>
/// After this face resolves, the next time a <see cref="DieType.Fire"/> face is committed this player turn,
/// all enemy-target <see cref="BurnEffectSO"/> stacks applied from that Fire resolve are doubled (including roll-watcher burn on that face).
/// </summary>
[Serializable]
public class PrimeNextFireRollDoubleEnemyBurnModifier : FaceResolveModifierBase
{
    public override void Modify(DieFaceSO face, FaceResult result, CombatManager combat, TurnRegistry registry)
    {
        registry.PendingNextFireRollDoubleEnemyBurn = true;
    }
}
