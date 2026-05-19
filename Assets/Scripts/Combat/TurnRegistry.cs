using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Volatile "blackboard" for the current player turn (before submit). Strength is read from player status effects, not stored here.
/// </summary>
public class TurnRegistry
{
    public int AccumulatedPhysicalDamage { get; private set; }
    public int AccumulatedArmor { get; private set; }
    public int BurnAppliedThisTurn { get; private set; }

    /// <summary>When true, the next committed <see cref="DieType.Fire"/> face doubles enemy-target <see cref="BurnEffectSO"/> stacks from that resolve (then cleared).</summary>
    public bool PendingNextFireRollDoubleEnemyBurn { get; set; }

    readonly HashSet<ActionVisualId> _activePlayerBarBuffs = new HashSet<ActionVisualId>();

    /// <summary>Non-status buff icons shown on the player status bar (keys into <see cref="GameIconIndexSO"/>).</summary>
    public IReadOnlyCollection<ActionVisualId> ActivePlayerBarBuffs => _activePlayerBarBuffs;

    /// <summary>Fired when <see cref="ActivePlayerBarBuffs"/> changes (player status bar refresh).</summary>
    public event Action OnPlayerBarBuffsChanged;

    /// <summary>
    /// Queued one-shot multiplier for the next eligible roll result this turn.
    /// Applied in <see cref="CombatManager"/> before the face is recorded.
    /// </summary>
    public bool NextRollMultiplierActive { get; set; }
    public bool NextRollMultiplyDamage { get; set; }
    public bool NextRollMultiplyArmor { get; set; }
    public float NextRollMultiplier { get; set; } = 1f;

    /// <summary>Supernova: if player busts, skip bust UI and apply this instead.</summary>
    public bool SupernovaBustOverrideActive { get; set; }
    public int SupernovaBustDamage { get; set; }

    public event Action<ElementType, int> OnValueAccumulated;

    public void SetPlayerBarBuff(ActionVisualId id, bool active)
    {
        if (id == ActionVisualId.None)
            return;

        var changed = active ? _activePlayerBarBuffs.Add(id) : _activePlayerBarBuffs.Remove(id);
        if (changed)
            OnPlayerBarBuffsChanged?.Invoke();
    }

    /// <summary>
    /// Clears volatile turn trackers. Call at new player turn (ResetTurn) and/or enemy turn start.
    /// Does not modify player Strength (status effects).
    /// </summary>
    public void ResetVolatile()
    {
        AccumulatedPhysicalDamage = 0;
        AccumulatedArmor = 0;
        BurnAppliedThisTurn = 0;
        PendingNextFireRollDoubleEnemyBurn = false;
        NextRollMultiplierActive = false;
        NextRollMultiplyDamage = false;
        NextRollMultiplyArmor = false;
        NextRollMultiplier = 1f;
        SupernovaBustOverrideActive = false;
        SupernovaBustDamage = 0;

        if (_activePlayerBarBuffs.Count > 0)
        {
            _activePlayerBarBuffs.Clear();
            OnPlayerBarBuffsChanged?.Invoke();
        }
    }

    public void RecordBurnApplied(int stacks)
    {
        if (stacks <= 0) return;
        BurnAppliedThisTurn += stacks;
        OnValueAccumulated?.Invoke(ElementType.Fire, stacks);
    }

    /// <summary>Called after resolve modifiers and queued physical double are applied, before channeled list is used for bust.</summary>
    public void RecordResolvedFace(FaceResult result)
    {
        if (result == null) return;

        if (result.Type == DieType.Damage && result.Damage > 0)
        {
            var totalPhysical = result.Damage * Mathf.Max(1, result.DamageAttackTimes);
            AccumulatedPhysicalDamage += totalPhysical;
            OnValueAccumulated?.Invoke(ElementType.Physical, totalPhysical);
        }

        if (result.Armor > 0)
        {
            AccumulatedArmor += result.Armor;
            OnValueAccumulated?.Invoke(ElementType.Defense, result.Armor);
        }

        if (result.Type == DieType.Curse && result.SelfDamage > 0)
            OnValueAccumulated?.Invoke(ElementType.Curse, result.SelfDamage);

        if (result.Type != DieType.Damage && result.Type != DieType.Armor && result.Type != DieType.Curse && result.Value > 0)
            OnValueAccumulated?.Invoke(ElementTypeExtensions.FromDieType(result.Type), result.Value);
    }

    /// <summary>Reverts <see cref="RecordResolvedFace"/> physical/armor totals (element value events are not mirrored here).</summary>
    public void UndoRecordResolvedFace(FaceResult result)
    {
        if (result == null) return;

        if (result.Type == DieType.Damage && result.Damage > 0)
        {
            var totalPhysical = result.Damage * Mathf.Max(1, result.DamageAttackTimes);
            AccumulatedPhysicalDamage -= totalPhysical;
            if (AccumulatedPhysicalDamage < 0) AccumulatedPhysicalDamage = 0;
        }

        if (result.Armor > 0)
        {
            AccumulatedArmor -= result.Armor;
            if (AccumulatedArmor < 0) AccumulatedArmor = 0;
        }
    }
}
