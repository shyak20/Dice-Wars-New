using UnityEngine;

/// <summary>
/// Extra element-pool row from a rolled face action (Burn stacks, Thorns, Max HP, etc.), separate from <see cref="FaceResult.Damage"/> / face value.
/// </summary>
public struct FacePoolExtraContribution
{
    public PoolRowKey PoolKey;
    public int Amount;
    public Sprite Icon;
    /// <summary>If set, this row is a deferred <see cref="ApplyStatusEffectAction"/> pending until submit — scales with Perfect Strike and bust rules.</summary>
    public ApplyStatusEffectAction PoolSourceAction;
    /// <summary>If set, scales with Perfect Strike like status rows; grant uses final <see cref="Amount"/> at submit.</summary>
    public MaxHpAction MaxHpPoolSource;
}
