using System;
using UnityEngine;

/// <summary>
/// Volatile "blackboard" for the current player turn (before submit). Strength is read from player status effects, not stored here.
/// </summary>
public class TurnRegistry
{
    public int AccumulatedPhysicalDamage { get; private set; }
    public int AccumulatedArmor { get; private set; }
    public int BurnAppliedThisTurn { get; private set; }

    /// <summary>Brute Force: next Physical damage face has its damage doubled once.</summary>
    public bool NextPhysicalDamageDoubled { get; set; }

    /// <summary>Supernova: if player busts, skip bust UI and apply this instead.</summary>
    public bool SupernovaBustOverrideActive { get; set; }
    public int SupernovaBustDamage { get; set; }

    public event Action<ElementType, int> OnValueAccumulated;

    /// <summary>
    /// Clears volatile turn trackers. Call at new player turn (ResetTurn) and/or enemy turn start.
    /// Does not modify player Strength (status effects).
    /// </summary>
    public void ResetVolatile()
    {
        AccumulatedPhysicalDamage = 0;
        AccumulatedArmor = 0;
        BurnAppliedThisTurn = 0;
        NextPhysicalDamageDoubled = false;
        SupernovaBustOverrideActive = false;
        SupernovaBustDamage = 0;
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
            AccumulatedPhysicalDamage += result.Damage;
            OnValueAccumulated?.Invoke(ElementType.Physical, result.Damage);
        }

        if (result.Armor > 0)
        {
            AccumulatedArmor += result.Armor;
            OnValueAccumulated?.Invoke(ElementType.Defense, result.Armor);
        }

        if (result.Type != DieType.Damage && result.Type != DieType.Armor && result.Value > 0)
            OnValueAccumulated?.Invoke(ElementTypeExtensions.FromDieType(result.Type), result.Value);
    }
}
