using UnityEngine;

[CreateAssetMenu(fileName = "Strength", menuName = "DiceGame/StatusEffects/Strength")]
public class StrengthEffectSO : StatusEffectSO
{
    /// <summary>Added only to attack <see cref="DieFaceSO.damage"/> on resolve — not to face pip / power. Uses Strength stacks from roll-batch start (see <see cref="CombatManager.GetStrengthStacksForCurrentRollBatch"/>).</summary>
    public override int GetPerDieAttackDamageBonus(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        return instance.Stacks;
    }
}
