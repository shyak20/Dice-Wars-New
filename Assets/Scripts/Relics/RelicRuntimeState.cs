/// <summary>Per-combat mutable state for relic actions (referenced from <see cref="GameActionContext.RelicRuntime"/>).</summary>
public sealed class RelicRuntimeState
{
    public bool DoubleFivePrimed;
    public bool FreeBustConsumed;
    public bool PerfectAtMaxPlusOneConsumed;
    public bool RuneTabletConsumedThisTurn;
    public int StormOrbStacksRemaining = -1;
    public RelicSO PendingDestroyRelicAfterBust;
}
