using UnityEngine;

[CreateAssetMenu(fileName = "Shadow", menuName = "DiceGame/StatusEffects/Shadow")]
public class ShadowEffectSO : StatusEffectSO
{
    public override int GetBonusAttack(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        if (GameActionDebug.Enabled)
            Debug.Log($"[Shadow] Adding +{instance.Stacks} bonus attack from Shadow stacks");
        return instance.Stacks;
    }
}
