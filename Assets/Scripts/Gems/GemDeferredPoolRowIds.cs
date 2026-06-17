/// <summary>
/// Stable <see cref="PoolRowKey.StableId"/> values for deferred gem effect rows in <see cref="GemCombatResolver"/>.
/// Keep in sync with <see cref="GameIconIndexSO.TryGetPoolRowBackground"/> action-background fallbacks.
/// </summary>
public static class GemDeferredPoolRowIds
{
    public const string Burn = "Gem Burn";
    public const string Heal = "Gem Heal";
    public const string Cleanse = "Gem Cleanse";
    public const string Gold = "Gem Gold";
    public const string MaxHp = "Gem Max HP";
    public const string Power = "Gem Power";
}
