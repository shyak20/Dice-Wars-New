using UnityEngine;

[CreateAssetMenu(fileName = "Strength", menuName = "DiceGame/StatusEffects/Strength")]
public class StrengthEffectSO : StatusEffectSO
{
    public override int ModifyFaceValue(StatusEffectInstance instance, StatusEffectContext ctx, int value)
    {
        if (value <= 0) return value;

        var bonus = instance.Stacks;
        if (GameActionDebug.Enabled)
            Debug.Log($"[Strength] +{bonus} to face value ({value} → {value + bonus})");

        return value + bonus;
    }
}
