using UnityEngine;

public abstract class StatusEffectSO : ScriptableObject
{
    public string effectName;
    [TextArea] public string description;
    public StatusEffectType type;
    public StatusEffectTarget target;
    public int stackDecayPerTurn;

    [Header("Player turn timing")]
    [InspectorName("Activate first")]
    [Tooltip("When enabled, applications of this status run before the enemy receives this turn's player physical damage. When disabled, they run immediately after that damage (still before enemy turn-start ticks such as burn). Immediate faces: the apply is held until after damage when disabled.")]
    [SerializeField] private bool activateBeforePlayerPhysicalDamage = true;

    /// <summary>When true, <see cref="CombatManager"/> applies this status before player physical damage to the enemy; when false, after.</summary>
    public bool ActivateBeforePlayerPhysicalDamage => activateBeforePlayerPhysicalDamage;

    public virtual void OnApply(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual void OnTurnStart(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual void OnBeforeEnemyTurn(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual int ModifyEnemyHitDamage(StatusEffectInstance instance, StatusEffectContext ctx, int damage) => damage;
    public virtual void OnAfterEnemyTurn(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual void OnPerfectStrike(StatusEffectInstance instance, StatusEffectContext ctx) { }
    public virtual int ModifyDamageToOwner(StatusEffectInstance instance, StatusEffectContext ctx, int damage) => damage;

    /// <summary>
    /// Enemy-only: modifies incoming damage from <see cref="EnemyDamagePresentationKind.Burn"/> hits (burn ticks, perfect strike burn, etc.).
    /// Default: unchanged. Physical pending attack uses <see cref="ModifyDamageToOwner"/> via <see cref="StatusEffectManager.ApplyDamageModifiers"/>.
    /// </summary>
    public virtual int ModifyBurnDamageToOwner(StatusEffectInstance instance, StatusEffectContext ctx, int damage) => damage;
    public virtual int GetBonusAttack(StatusEffectInstance instance, StatusEffectContext ctx) => 0;
    /// <summary>Added to <see cref="DieFaceSO.damage"/> when the player resolves an attacking die, and to each enemy physical strike base damage before <see cref="ModifyEnemyHitDamage"/> (e.g. Strength).</summary>
    public virtual int GetPerDieAttackDamageBonus(StatusEffectInstance instance, StatusEffectContext ctx) => 0;
    public virtual int ModifyFaceValue(StatusEffectInstance instance, StatusEffectContext ctx, int value) => value;
    public virtual bool ShouldRedirectAttackToSelf(StatusEffectInstance instance, StatusEffectContext ctx) => false;
    public virtual void OnRemove(StatusEffectInstance instance, StatusEffectContext ctx) { }
}
