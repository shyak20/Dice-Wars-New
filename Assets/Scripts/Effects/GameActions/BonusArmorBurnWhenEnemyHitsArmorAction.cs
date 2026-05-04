using System;
using UnityEngine;

/// <summary>
/// Deferred (turn submit): grants bonus armor to the element pool, then during the enemy turn each point of player armor
/// lost to <see cref="PlayerDamageSource.EnemyPhysicalAttack"/> applies <see cref="burnStacksPerArmorLost"/> burn to the enemy.
/// Keep <see cref="GameActionWithIcon.ActivateImmediately"/> off so armor and registration run at turn end before the orb/armor grant.
/// </summary>
[Serializable]
public class BonusArmorBurnWhenEnemyHitsArmorAction : GameActionWithIcon
{
    [SerializeField, Min(0)] private int bonusArmor = 10;

    [Tooltip("Enemy burn applied per point of player armor destroyed by enemy physical hits this enemy turn.")]
    [SerializeField, Min(1)]
    private int burnStacksPerArmorLost = 1;

    [Tooltip("Must target Enemy.")]
    [SerializeField] private BurnEffectSO burnDefinition;

    public int BonusArmor => bonusArmor;
    public int BurnStacksPerArmorLost => burnStacksPerArmorLost;

    protected override ActionVisualId VisualKey => ActionVisualId.BonusArmorBurnWhenStruck;

    public override void Execute(GameActionContext context)
    {
        if (context?.CombatManager == null) return;
        if (burnDefinition == null)
        {
            Debug.LogError("BonusArmorBurnWhenEnemyHitsArmorAction: assign burnDefinition (enemy-target Burn).");
            return;
        }

        if (burnStacksPerArmorLost <= 0) return;

        if (bonusArmor > 0)
            context.CombatManager.AddBonusArmorFromAction(bonusArmor);

        context.CombatManager.RegisterBurnOnPlayerArmorLostFromEnemyPhysical(burnDefinition, burnStacksPerArmorLost);

        if (GameActionDebug.Enabled)
            Debug.Log($"[BonusArmorBurnWhenStruck] +{bonusArmor} pending armor; {burnStacksPerArmorLost} burn per armor lost to enemy hits.");
    }
}
