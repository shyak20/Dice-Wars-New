using UnityEngine;

public abstract class StatusEffectSO : ScriptableObject
{
    public string effectName;
    [TextArea] public string description;
    public StatusEffectType type;
    public StatusEffectTarget target;
    public int stackDecayPerTurn;

    public virtual void OnApply(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual void OnTurnStart(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual void OnBeforeEnemyTurn(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual int ModifyEnemyHitDamage(StatusEffectInstance instance, StatusEffectContext ctx, int damage) => damage;
    public virtual void OnAfterEnemyTurn(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual void OnPerfectStrike(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual int ModifyDamageToOwner(StatusEffectInstance instance, StatusEffectContext ctx, int damage) => damage;
    public virtual int GetBonusAttack(StatusEffectInstance instance, StatusEffectContext ctx) => 0;
    /// <summary>Added to <see cref="DieFaceSO.damage"/> each time the player resolves an attacking die (face.damage &gt; 0).</summary>
    public virtual int GetPerDieAttackDamageBonus(StatusEffectInstance instance, StatusEffectContext ctx) => 0;
    public virtual int ModifyFaceValue(StatusEffectInstance instance, StatusEffectContext ctx, int value) => value;
    public virtual bool ShouldRedirectAttackToSelf(StatusEffectInstance instance, StatusEffectContext ctx) => false;
    public virtual void OnRemove(StatusEffectInstance instance, StatusEffectContext ctx) { }

    /// <summary>
    /// When applied via <see cref="ApplyStatusEffectAction"/> on a rolled face, add a separate flyout / element-pool row
    /// (e.g. Burn stacks on Fire), not merged into physical <see cref="FaceResult.Damage"/>.
    /// </summary>
    /// <param name="displayedStacks">Stacks after rules like Pyromaniac (caller computes).</param>
    public virtual bool TryGetRollFlyoutContribution(int displayedStacks, StatusEffectTarget applyTarget, out DieType poolType, out int poolAmount)
    {
        poolType = default;
        poolAmount = 0;
        return false;
    }
}
