/// <summary>Passed to <see cref="PlayerStatus.TakeDamage"/> so hit feedback can distinguish enemy strikes from other sources.</summary>
public enum PlayerDamageSource
{
    Generic,
    EnemyPhysicalAttack,
    /// <summary>Damage from the enemy's Thorns when the player deals attack damage (see <see cref="CombatManager"/>).</summary>
    ThornsRetaliation,
    /// <summary>Damage from rolled curse faces when you end your turn (<see cref="CombatManager"/>).</summary>
    CurseFace,
}
