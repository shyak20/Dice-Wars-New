using UnityEngine;

[CreateAssetMenu(fileName = "Strength", menuName = "DiceGame/StatusEffects/Strength")]
public class StrengthEffectSO : StatusEffectSO
{
    public override int GetBonusAttack(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        if (GameActionDebug.Enabled)
            Debug.Log($"[Strength] Adding +{instance.Stacks} bonus attack from Strength stacks");
        return instance.Stacks;
    }
}
