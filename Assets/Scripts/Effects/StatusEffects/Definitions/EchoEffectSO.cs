using UnityEngine;

/// <summary>
/// Each stack: the <b>next</b> time the player presses Roll, that entire roll batch adds no value to the power meter
/// (dice still resolve for damage/armor/face effects). One stack is consumed when the roll batch starts.
/// Must not decay per turn or <see cref="StatusEffectManager.TickTurnStart"/> removes stacks before the free roll.
/// </summary>
[CreateAssetMenu(fileName = "Echo", menuName = "DiceGame/StatusEffects/Echo")]
public class EchoEffectSO : StatusEffectSO
{
#if UNITY_EDITOR
    private void OnValidate()
    {
        stackDecayPerTurn = 0;
    }
#endif
}
