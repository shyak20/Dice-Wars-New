using System;
using UnityEngine;

/// <summary>Decoupled gameplay signals for meta trial progression (subscribe from <see cref="ProgressionManager"/> only).</summary>
public static class ProgressionEvents
{
    public static Action<string, EnemyRank> OnEnemyDefeated;
    public static Action<int, ProgressionCoinSpendSource> OnCoinsSpent;
    public static Action OnPerfectCast;
    public static Action<int> OnDamageBlocked;
    public static Action OnMapTileMoved;
    public static Action<int> OnHpLost;
    public static Action<int> OnPhysicalDamageDealt;
    public static Action<int> OnFireDamageDealt;
    public static Action<int> OnExactRoll;
    public static Action<int> OnAccumulatedPower;
    public static Action OnCastOverload;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => ClearAll();

    public static void ClearAll()
    {
        OnEnemyDefeated = null;
        OnCoinsSpent = null;
        OnPerfectCast = null;
        OnDamageBlocked = null;
        OnMapTileMoved = null;
        OnHpLost = null;
        OnPhysicalDamageDealt = null;
        OnFireDamageDealt = null;
        OnExactRoll = null;
        OnAccumulatedPower = null;
        OnCastOverload = null;
    }
}
