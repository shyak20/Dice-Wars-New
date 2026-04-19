using UnityEngine;

[CreateAssetMenu(fileName = "Strength", menuName = "DiceGame/StatusEffects/Strength")]
public class StrengthEffectSO : StatusEffectSO
{
    /// <summary>Added only to attack <see cref="DieFaceSO.damage"/> on resolve — not to face pip / power (see <see cref="CombatManager.CommitResolvedRoll"/>).</summary>
    public override int GetPerDieAttackDamageBonus(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        return instance.Stacks;
    }
}
