/// <summary>Queued rule applied by <see cref="CombatManager"/> when a settled face matches <see cref="RequiredFaceValue"/>.</summary>
public sealed class ValueBasedRollWatcherEntry
{
    public int RequiredFaceValue;
    public RollBonusType BonusType;
    public int Amount;
    public BurnEffectSO BurnDefinition;
    /// <summary>Relic scheduling: first <see cref="CombatManager.CurrentRollBatchId"/> (inclusive). 0 = do not gate on batch.</summary>
    public int FirstEligibleBatchId;
    /// <summary>Face scheduling: first monotonic resolve index (inclusive). 0 = do not gate on resolve index.</summary>
    public int FirstEligibleResolveSequence;
}
