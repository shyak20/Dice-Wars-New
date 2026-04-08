using UnityEngine;

/// <summary>
/// Extra element-pool row from a rolled face action (e.g. Burn stacks → Fire), separate from <see cref="FaceResult.Damage"/> / face value.
/// </summary>
public struct FacePoolExtraContribution
{
    public DieType PoolType;
    public int Amount;
    public Sprite Icon;
}
