using UnityEngine;

/// <summary>
/// Player buff: each stack adds +1 to enemy Burn applications this turn (see <see cref="ApplyStatusEffectAction"/>).
/// Grant stacks via <see cref="GrantPyromaniacModifier"/> on die faces.
/// </summary>
[CreateAssetMenu(fileName = "Pyromaniac", menuName = "DiceGame/StatusEffects/Pyromaniac")]
public class PyromaniacEffectSO : StatusEffectSO
{
}
