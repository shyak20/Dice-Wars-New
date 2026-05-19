using UnityEngine;

/// <summary>
/// Extra element-pool row from a rolled face action (Burn stacks, Thorns, Max HP, etc.), separate from <see cref="FaceResult.Damage"/> / face value.
/// </summary>
public struct FacePoolExtraContribution
{
    public PoolRowKey PoolKey;
    public int Amount;
    public Sprite Icon;
    /// <summary>When set, used for the stored-actions row frame instead of <see cref="GameIconIndexSO.TryGetPoolRowBackground"/> on <see cref="PoolKey"/> (supports custom <c>poolRowId</c> on heal / max HP, etc.).</summary>
    public Sprite PoolRowBackground;
    /// <summary>If set, this row is a deferred <see cref="ApplyStatusEffectAction"/> pending until submit — scales with Perfect Strike and bust rules.</summary>
    public ApplyStatusEffectAction PoolSourceAction;
    /// <summary>If set, scales with Perfect Strike like status rows; grant uses final <see cref="Amount"/> at submit.</summary>
    public MaxHpAction MaxHpPoolSource;
    /// <summary>Generic switch for deferred rows (e.g. gem stacks) that should scale with Perfect Strike.</summary>
    public bool PerfectStrikeScales;
    /// <summary>When non-zero, this row belongs to one deferred gem effect execution and can be resolved exactly at turn-end.</summary>
    public int GemDeferredHandleId;
    /// <summary>If true, this row is nulled when bust chooses Nullify Damage.</summary>
    public bool CancelOnBustNullifyDamage;
    /// <summary>If true, this row is nulled when bust chooses Nullify Armor.</summary>
    public bool CancelOnBustNullifyArmor;
    /// <summary>
    /// When true, this row exists only for die flyout visuals (e.g. immediate <see cref="ApplyStatusEffectAction"/>).
    /// Omitted from <see cref="CombatManager.BuildStoredActionsPool"/> pending totals; use a short resync after flyout to clear display drift.
    /// </summary>
    public bool VisualFlyoutOnly;
    /// <summary>When true with <see cref="VisualFlyoutOnly"/>, fly to the player status bar instead of the element pool.</summary>
    public bool FlyToPlayerStatusBar;
}
