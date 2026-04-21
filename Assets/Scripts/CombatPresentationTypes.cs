using UnityEngine;

/// <summary>Where the power orb resolved for <see cref="CombatEvents.OnPowerOrbImpact"/>.</summary>
public enum PowerOrbImpactTarget
{
    Enemy,
    PlayerSupport
}

/// <summary>Payload when the power orb reaches its anchor (before immediate physical damage from the same turn).</summary>
public readonly struct PowerOrbImpactPayload
{
    public readonly PowerOrbImpactTarget Target;
    public readonly Vector3 WorldPosition;
    public readonly EnemyController Enemy;

    public PowerOrbImpactPayload(PowerOrbImpactTarget target, Vector3 worldPosition, EnemyController enemy)
    {
        Target = target;
        WorldPosition = worldPosition;
        Enemy = enemy;
    }
}

/// <summary>Channel for enemy damage popups / hit flashes (<see cref="CombatEvents.OnEnemyDamagePresentation"/>).</summary>
public enum EnemyDamagePresentationKind
{
    Physical,
    Burn
}
