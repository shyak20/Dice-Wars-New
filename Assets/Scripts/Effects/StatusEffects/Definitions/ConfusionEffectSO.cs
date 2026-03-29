using UnityEngine;

[CreateAssetMenu(fileName = "Confusion", menuName = "DiceGame/StatusEffects/Confusion")]
public class ConfusionEffectSO : StatusEffectSO
{
    [SerializeField, Range(1, 100)] private int chancePerStack = 10;

    public override bool ShouldRedirectAttackToSelf(StatusEffectInstance instance, StatusEffectContext ctx)
    {
        var totalChance = Mathf.Min(100, chancePerStack * instance.Stacks);
        var roll = Random.Range(0, 100);
        var redirected = roll < totalChance;

        if (GameActionDebug.Enabled)
            Debug.Log($"[Confusion] {totalChance}% chance ({instance.Stacks} stacks × {chancePerStack}%), rolled {roll} — {(redirected ? "REDIRECTED to self" : "normal attack")}");

        return redirected;
    }
}
