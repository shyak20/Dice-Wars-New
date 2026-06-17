/// <summary>
/// Whether a relic can be drafted or granted for the active hero run.
/// </summary>
public static class RelicHeroEligibility
{
    public static bool IsDraftableForHero(RelicSO relic, PlayerDataSO activeHero)
    {
        if (relic == null)
            return false;
        if (relic.IsGenericRelic)
            return true;
        return activeHero != null && relic.ExclusiveHero == activeHero;
    }
}
