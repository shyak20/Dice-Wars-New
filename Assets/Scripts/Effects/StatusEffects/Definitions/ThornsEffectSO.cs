using UnityEngine;

/// <summary>
/// Retaliation stacks: when this unit takes physical attack damage from the opposing side, the attacker takes damage equal to total stacks (1 per stack per qualifying hit).
/// Apply with <see cref="ThornsAction"/> (references a Thorns asset) or <see cref="ApplyStatusEffectAction"/>.
/// Set <see cref="StatusEffectSO.target"/> to Player or Enemy for who holds the buff.
/// </summary>
[CreateAssetMenu(fileName = "Thorns", menuName = "DiceGame/StatusEffects/Thorns")]
public class ThornsEffectSO : StatusEffectSO
{
}
