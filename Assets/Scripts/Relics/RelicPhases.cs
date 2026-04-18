/// <summary>Phases passed in <see cref="GameActionContext.RelicPhase"/> for relic <see cref="IGameAction"/> execution.</summary>
public static class RelicPhases
{
    public const string CombatStart = "Relic_CombatStart";

    /// <summary>After the enemy finishes their turn; next line is a new player turn (rolls reset). Used to re-apply per-turn relic watchers.</summary>
    public const string AfterEnemyTurnPlayerTurnStart = "Relic_AfterEnemyTurnPlayerTurnStart";

    public const string QueryMaxPowerBonus = "Relic_QueryMaxPowerBonus";
    public const string QueryMapMoveBonus = "Relic_QueryMapMoveBonus";
    public const string QueryPerfectAtMaxMinusOne = "Relic_QueryPerfectAtMaxMinusOne";
    public const string QueryPerfectStrikeMultiplier = "Relic_QueryPerfectStrikeMultiplier";
    public const string QueryFreeBustRelicCount = "Relic_QueryFreeBustRelicCount";

    /// <summary>Face result built; mutate damage/armor before channeling.</summary>
    public const string ModifyFaceResult = "Relic_ModifyFaceResult";

    /// <summary>After power was increased by the current roll; <see cref="GameActionContext.TriggeringFace"/> set.</summary>
    public const string AfterPowerChangedFromRoll = "Relic_AfterPowerChangedFromRoll";

    public const string OnPerfectStrike = "Relic_OnPerfectStrike";

    /// <summary>Player is over max power; if any action sets <see cref="GameActionContext.RelicBoolAccumulator"/>, bust UI may be skipped once.</summary>
    public const string TryConsumeFreeBust = "Relic_TryConsumeFreeBust";
}
