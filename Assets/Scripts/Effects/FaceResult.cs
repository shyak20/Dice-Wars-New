using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class FaceResult
{
    public DieFaceSO Face { get; set; }
    public int Value { get; set; }
    public DieType Type { get; set; }

    /// <summary>Optional: physical die instance that produced this result (used for reroll picking).</summary>
    public Transform DieSource { get; set; }

    /// <summary>Die asset from the roll batch that produced this resolve (gems, socket effects).</summary>
    public DieAssetSO SourceDieAsset { get; set; }

    /// <summary>Index within the current roll batch spawn order.</summary>
    public int BatchGatherIndex { get; set; } = -1;

    /// <summary>When set, <see cref="RerollDieAction"/> with <c>RerollTriggeringDieOnly</c> runs after this face's outcomes are submitted.</summary>
    public bool AwaitingPostSubmitTriggeringReroll { get; set; }

    /// <summary>First-pass resolve for a die that will be physics-rerolled after the whole batch submits; power is skipped on this resolve.</summary>
    public bool AwaitingPostBatchOtherDiceReroll { get; set; }

    /// <summary>Power actually added for this face (0 when Echo skips power for the batch).</summary>
    public int PowerContributionThisResolve { get; set; }

    /// <summary>How much <see cref="CombatManager"/> kinetic shield bonus was incremented for this resolve (0 or 1).</summary>
    public int KineticShieldBonusContribution { get; set; }

    // New fields to track independent values and timing
    /// <summary>Per-hit physical after Strength, watchers, relics, and face modifiers.</summary>
    public int Damage { get; set; }
    /// <summary>For <see cref="DieType.Damage"/> faces: number of times <see cref="Damage"/> applies (default 1).</summary>
    public int DamageAttackTimes { get; set; } = 1;
    /// <summary>Total physical from this resolve: <see cref="Damage"/> × <see cref="DamageAttackTimes"/> when type is Damage and <see cref="Damage"/> &gt; 0.</summary>
    public int TotalDamageContribution => Type == DieType.Damage && Damage > 0
        ? Damage * Mathf.Max(1, DamageAttackTimes)
        : 0;

    public int Armor { get; set; }

    public int SelfDamage { get; set; }

    /// <summary>Pool row total for self-hit on the player (curse face value and/or <see cref="DealPlayerDamageOnSubmitAction"/>).</summary>
    public int TotalSelfDamageContribution => Mathf.Max(0, SelfDamage);

    /// <summary>Set when this resolve is the next Fire face after <see cref="TurnRegistry.PendingNextFireRollDoubleEnemyBurn"/>; doubles enemy burn stacks applied from this face only.</summary>
    public bool DoubleEnemyBurnStacksThisResolve { get; set; }

    /// <summary>When <see cref="DoubleEnemyBurnStacksThisResolve"/> and <paramref name="appliedStatus"/> is enemy-target burn, returns <paramref name="stacks"/> × 2; otherwise unchanged.</summary>
    public int ApplyFireDoubleToEnemyBurnStacks(int stacks, StatusEffectSO appliedStatus)
    {
        if (stacks <= 0 || appliedStatus == null || !DoubleEnemyBurnStacksThisResolve) return stacks;
        if (appliedStatus is BurnEffectSO b && b.target == StatusEffectTarget.Enemy) return stacks * 2;
        return stacks;
    }

    /// <summary>Overload for burn-only apply paths (e.g. roll watcher).</summary>
    public int ApplyFireDoubleToEnemyBurnStacks(int stacks, BurnEffectSO burnDefinition)
    {
        if (burnDefinition == null || stacks <= 0 || !DoubleEnemyBurnStacksThisResolve) return stacks;
        if (burnDefinition.target == StatusEffectTarget.Enemy) return stacks * 2;
        return stacks;
    }

    /// <summary>Copied from the rolled face; <see cref="IGameAction.ActivateImmediately"/> controls gather vs turn-end <see cref="IGameAction.Execute"/>, and early vs late <see cref="FaceResolveModifierBase.Modify"/>.</summary>
    public List<IGameAction> Actions { get; set; } = new List<IGameAction>();

    /// <summary>Multi-enemy: the enemy the <b>damage piece</b> of this face was assigned to (drag-and-drop, or auto-assigned when only one
    /// enemy is alive). Null until assigned; resolution falls back to the primary enemy. Each enemy-targeted action is targeted
    /// independently via <see cref="SetActionTarget"/> so one face can hit different enemies (e.g. damage on A, Burn on B).
    /// When <see cref="UsesSplitDamageHits"/>, use <see cref="SetDamageHitTarget"/> / <see cref="GetDamageHitTarget"/> instead.</summary>
    public EnemyController DamageTargetEnemy { get; set; }

    private EnemyController[] _damageHitTargets;

    /// <summary><see cref="DieType.Damage"/> with <see cref="DamageAttackTimes"/> &gt; 1 — one assignable element value per hit.</summary>
    public bool UsesSplitDamageHits => Type == DieType.Damage && Damage > 0 && DamageAttackTimes > 1;

    public void SetDamageHitTarget(int hitIndex, EnemyController enemy)
    {
        if (hitIndex < 0)
            return;

        EnsureDamageHitTargetArray();
        if (hitIndex >= _damageHitTargets.Length)
            return;

        _damageHitTargets[hitIndex] = enemy;
    }

    public EnemyController GetDamageHitTarget(int hitIndex)
    {
        if (hitIndex < 0 || _damageHitTargets == null || hitIndex >= _damageHitTargets.Length)
            return null;

        return _damageHitTargets[hitIndex];
    }

    void EnsureDamageHitTargetArray()
    {
        var count = Mathf.Max(1, DamageAttackTimes);
        if (_damageHitTargets == null || _damageHitTargets.Length != count)
            _damageHitTargets = new EnemyController[count];
    }

    private Dictionary<IGameAction, EnemyController> _actionTargets;

    /// <summary>Multi-enemy: assign which enemy a specific enemy-targeted action (e.g. Burn) on this face resolves against.</summary>
    public void SetActionTarget(IGameAction action, EnemyController enemy)
    {
        if (action == null) return;
        _actionTargets ??= new Dictionary<IGameAction, EnemyController>();
        _actionTargets[action] = enemy;
    }

    /// <summary>Enemy assigned to a specific action via <see cref="SetActionTarget"/>, or null when unassigned.</summary>
    public EnemyController GetActionTarget(IGameAction action)
    {
        if (action == null || _actionTargets == null) return null;
        return _actionTargets.TryGetValue(action, out var e) ? e : null;
    }

    /// <summary>Roll batch (player roll command) that produced this face; used to find the newly rolled outcomes awaiting assignment.</summary>
    public int BatchId { get; set; }

    /// <summary>Copied from <see cref="DieFaceSO.AttackAllEnemies"/> — damage and enemy debuffs apply to every alive enemy.</summary>
    public bool AttackAllEnemies => Face != null && Face.AttackAllEnemies;

    /// <summary>
    /// True when any part of this resolve targets an enemy (physical/element damage, or an enemy-target status like Burn) and therefore
    /// must be assigned to a specific enemy. Player-only buffs (armor, heal, max HP, cleanse, curse self-damage) return false.
    /// </summary>
    public bool IsEnemyTargeted
    {
        get
        {
            if ((Type == DieType.Damage || Type == DieType.Fire || Type == DieType.Ice || Type == DieType.Nature) && Damage > 0)
                return true;

            if (Actions != null)
            {
                foreach (var a in Actions)
                {
                    if (a is FaceResolveModifierBase) continue;
                    if (a is ApplyStatusEffectAction apply &&
                        apply.StatusEffectDefinition != null &&
                        apply.StatusEffectDefinition.target == StatusEffectTarget.Enemy)
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>True when this face's direct damage is an enemy-targeted element (physical/fire/ice/nature with damage).</summary>
    public bool HasEnemyDamagePiece =>
        (Type == DieType.Damage || Type == DieType.Fire || Type == DieType.Ice || Type == DieType.Nature) && Damage > 0;

    /// <summary>Deferred-action rows for <see cref="StoredActionsPoolDisplay"/> (ApplyStatusEffect, Thorns, Max HP, etc.); filled before this face is added to channeled faces.</summary>
    public List<FacePoolExtraContribution> ActionPoolContributions { get; } = new List<FacePoolExtraContribution>();

    /// <summary>When set, primary damage/armor flyout rows from this face show this relic/gem icon briefly.</summary>
    public Sprite BuffSourceIcon { get; set; }

    HashSet<long> _cancelledPhysicalKeys;
    HashSet<long> _cancelledActionKeys;

    /// <summary>
    /// Marks a physical damage piece cancelled on arrival (Ghostwalk).
    /// Use <paramref name="hitIndex"/> &gt;= 0 for split-hit lines; -1 cancels the whole piece for that enemy.
    /// </summary>
    public void MarkPhysicalCancelled(EnemyController enemy, int hitIndex = -1)
    {
        if (enemy == null)
            return;

        _cancelledPhysicalKeys ??= new HashSet<long>();
        _cancelledPhysicalKeys.Add(PackEnemyHitKey(enemy, hitIndex));
        if (hitIndex >= 0)
            return;

        // Whole-piece cancel also covers every split hit against this enemy.
        if (UsesSplitDamageHits)
        {
            for (var hit = 0; hit < DamageAttackTimes; hit++)
                _cancelledPhysicalKeys.Add(PackEnemyHitKey(enemy, hit));
        }
    }

    public bool IsPhysicalCancelled(EnemyController enemy, int hitIndex = -1)
    {
        if (enemy == null || _cancelledPhysicalKeys == null || _cancelledPhysicalKeys.Count == 0)
            return false;

        if (_cancelledPhysicalKeys.Contains(PackEnemyHitKey(enemy, -1)))
            return true;

        return hitIndex >= 0 && _cancelledPhysicalKeys.Contains(PackEnemyHitKey(enemy, hitIndex));
    }

    public void MarkActionCancelled(IGameAction action, EnemyController enemy)
    {
        if (action == null || enemy == null)
            return;

        _cancelledActionKeys ??= new HashSet<long>();
        _cancelledActionKeys.Add(PackActionEnemyKey(action, enemy));
    }

    public bool IsActionCancelled(IGameAction action, EnemyController enemy)
    {
        if (action == null || enemy == null || _cancelledActionKeys == null || _cancelledActionKeys.Count == 0)
            return false;

        return _cancelledActionKeys.Contains(PackActionEnemyKey(action, enemy));
    }

    static long PackEnemyHitKey(EnemyController enemy, int hitIndex)
    {
        unchecked
        {
            return ((long)enemy.GetInstanceID() << 32) ^ (uint)hitIndex;
        }
    }

    static long PackActionEnemyKey(IGameAction action, EnemyController enemy)
    {
        unchecked
        {
            return ((long)RuntimeHelpers.GetHashCode(action) << 32) ^ (uint)enemy.GetInstanceID();
        }
    }
}
