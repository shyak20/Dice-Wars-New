using UnityEngine;

/// <summary>Meta progression trial objectives.</summary>
public enum TrialType
{
    [InspectorName("Monsters Killed")]
    MonstersKilled,

    [InspectorName("Coins Spend")]
    CoinsSpend,

    [InspectorName("Perfect Cast")]
    PerfectCast,

    [InspectorName("Damage Blocked")]
    DamageBlocked,

    [InspectorName("Moves on Map")]
    MovesOnMap,

    [InspectorName("Boss Kills")]
    BossKills,

    [InspectorName("Elite Kills")]
    EliteKills,

    [InspectorName("HP Lost")]
    HpLost,

    [InspectorName("Physical Damage Dealt")]
    PhysicalDamageDealt,

    [InspectorName("Fire Damage Dealt")]
    FireDamageDealt,

    [InspectorName("Exact Roll")]
    ExactRoll,

    [InspectorName("Accumulated Power")]
    AccumulatedPower,

    [InspectorName("Cast Overload")]
    CastOverload
}

/// <summary>Where run gold was spent for <see cref="TrialType.CoinsSpend"/> trials.</summary>
public enum ProgressionCoinSpendSource
{
    Shop,
    UnknownMapEvent
}
