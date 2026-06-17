using UnityEngine;

/// <summary>
/// Forwards gameplay signals into <see cref="ProgressionEvents"/>. Place in bootstrap scene beside <see cref="RunManager"/>.
/// </summary>
public sealed class ProgressionEventBridge : MonoBehaviour
{
    public static ProgressionEventBridge Instance { get; private set; }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void NotifyCombatVictory(EnemyTypeSO enemy)
    {
        if (enemy == null)
            return;

        var contentId = ProgressionContentIds.For(enemy);
        ProgressionEvents.OnEnemyDefeated?.Invoke(contentId, enemy.enemyRank);
    }

    public static void NotifyPerfectCast() => ProgressionEvents.OnPerfectCast?.Invoke();

    public static void NotifyDamageBlocked(int armorGained) =>
        ProgressionEvents.OnDamageBlocked?.Invoke(Mathf.Max(0, armorGained));

    public static void NotifyMapTileMoved() => ProgressionEvents.OnMapTileMoved?.Invoke();

    public static void NotifyCoinsSpent(int amount, ProgressionCoinSpendSource source) =>
        ProgressionEvents.OnCoinsSpent?.Invoke(Mathf.Max(0, amount), source);

    public static void NotifyHpLost(int hpLost) =>
        ProgressionEvents.OnHpLost?.Invoke(Mathf.Max(0, hpLost));

    public static void NotifyPhysicalDamageDealt(int amount) =>
        ProgressionEvents.OnPhysicalDamageDealt?.Invoke(Mathf.Max(0, amount));

    public static void NotifyFireDamageDealt(int amount) =>
        ProgressionEvents.OnFireDamageDealt?.Invoke(Mathf.Max(0, amount));

    public static void NotifyExactRoll(int rolledFaceValue) =>
        ProgressionEvents.OnExactRoll?.Invoke(rolledFaceValue);

    public static void NotifyAccumulatedPower(int powerFromFace) =>
        ProgressionEvents.OnAccumulatedPower?.Invoke(Mathf.Max(0, powerFromFace));

    public static void NotifyCastOverload() => ProgressionEvents.OnCastOverload?.Invoke();
}
