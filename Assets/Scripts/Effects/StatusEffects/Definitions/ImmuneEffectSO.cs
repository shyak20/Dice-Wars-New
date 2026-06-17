using UnityEngine;

/// <summary>
/// While stacks &gt; 0, enemy hits on the player are capped at 1 damage per hit (see <see cref="CombatManager"/> enemy turn).
/// Each such hit consumes one stack (<see cref="StatusEffectManager.ConsumeImmuneStackAfterHit"/>).
/// Turn-based decay is disabled (stack decay per turn should be 0 on the asset).
/// </summary>
[CreateAssetMenu(fileName = "Immune", menuName = "DiceGame/StatusEffects/Immune")]
public class ImmuneEffectSO : StatusEffectSO
{
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stackDecayPerTurn != 0)
            stackDecayPerTurn = 0;
    }
#endif
}
