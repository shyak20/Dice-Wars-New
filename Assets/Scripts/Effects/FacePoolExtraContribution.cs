using UnityEngine;

/// <summary>
/// Extra element-pool row from a rolled face action (e.g. Burn stacks → Fire), separate from <see cref="FaceResult.Damage"/> / face value.
/// </summary>
public struct FacePoolExtraContribution
{
    public DieType PoolType;
    public int Amount;
    public Sprite Icon;
    /// <summary>If set, this row is a deferred <see cref="ApplyStatusEffectAction"/> pending until submit — scales with Perfect Strike and bust rules.</summary>
    public ApplyStatusEffectAction PoolSourceAction;
    /// <summary>If set, scales with Perfect Strike like status rows; grant uses final <see cref="Amount"/> at submit.</summary>
    public MaxHpAction MaxHpPoolSource;
}
