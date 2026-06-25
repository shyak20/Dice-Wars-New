using System;
using UnityEngine;

public enum IncreaseOtherElementsTargetFilter
{
    /// <summary>Any other die that contributes at least one element pool row this resolve.</summary>
    Any = 0,
    /// <summary>Dice with a pool row for <see cref="IncreaseOtherElementsAction.targetDieType"/>.</summary>
    DieType = 1,
    /// <summary>Dice contributing stacks of <see cref="IncreaseOtherElementsAction.targetStatusEffect"/> (e.g. Burn).</summary>
    StatusEffect = 2,
}

/// <summary>
/// After every die in the roll batch has raised its flyout, adds a configured bonus to matching <b>other</b> dice'
/// attack / defense / status pool rows without changing face value or Cast Power. Launches die-to-die projectiles;
/// target flyout amounts update on arrival before those rows fly to the element pool.
/// </summary>
[Serializable]
public class IncreaseOtherElementsAction : GameActionWithIcon
{
    [SerializeField] private IncreaseOtherElementsTargetFilter targetFilter = IncreaseOtherElementsTargetFilter.Any;
    [SerializeField] private DieType targetDieType = DieType.Fire;
    [SerializeField] private StatusEffectSO targetStatusEffect;
    [SerializeField, Min(1)] private int bonusAmount = 1;
    [SerializeField, Tooltip("When on, only one random matching die in the batch receives the bonus.")]
    private bool onlyOneRandomTarget;

    public IncreaseOtherElementsTargetFilter TargetFilter => targetFilter;
    public DieType TargetDieType => targetDieType;
    public StatusEffectSO TargetStatusEffect => targetStatusEffect;
    public int BonusAmount => bonusAmount;
    public bool OnlyOneRandomTarget => onlyOneRandomTarget;

    protected override ActionVisualId VisualKey => ActionVisualId.IncreaseOtherElements;

    public override void Execute(GameActionContext context)
    {
    }

    public static bool FaceHasAction(DieFaceSO face)
    {
        if (face?.actions == null)
            return false;

        for (var i = 0; i < face.actions.Count; i++)
        {
            if (face.actions[i] is IncreaseOtherElementsAction)
                return true;
        }

        return false;
    }

    public static bool TryGetMatchingPoolRow(FaceResult face, IncreaseOtherElementsAction action, out PoolRowKey rowKey)
    {
        rowKey = default;
        if (face == null || action == null)
            return false;

        switch (action.TargetFilter)
        {
            case IncreaseOtherElementsTargetFilter.Any:
                return TryGetFirstPoolRow(face, out rowKey);
            case IncreaseOtherElementsTargetFilter.DieType:
                return TryGetPoolRowForDieType(face, action.TargetDieType, out rowKey);
            case IncreaseOtherElementsTargetFilter.StatusEffect:
                return TryGetPoolRowForStatus(face, action.TargetStatusEffect, out rowKey);
            default:
                return false;
        }
    }

    public static void ApplyBonusToFace(FaceResult face, PoolRowKey rowKey, int bonus)
    {
        if (face == null || bonus <= 0)
            return;

        if (PoolRowKey.TryGetDieType(rowKey, out var dieType))
        {
            switch (dieType)
            {
                case DieType.Damage:
                    if (face.Damage > 0)
                    {
                        var times = Mathf.Max(1, face.DamageAttackTimes);
                        face.Damage += (bonus + times - 1) / times;
                    }
                    break;
                case DieType.Armor:
                    face.Armor += bonus;
                    break;
                case DieType.Curse:
                    face.SelfDamage += bonus;
                    break;
            }

            return;
        }

        for (var i = 0; i < face.ActionPoolContributions.Count; i++)
        {
            var contribution = face.ActionPoolContributions[i];
            if (!contribution.PoolKey.Equals(rowKey))
                continue;

            contribution.Amount += bonus;
            face.ActionPoolContributions[i] = contribution;
            return;
        }
    }

    static bool TryGetFirstPoolRow(FaceResult face, out PoolRowKey rowKey)
    {
        rowKey = default;
        if (face == null)
            return false;

        if (face.TotalDamageContribution > 0)
        {
            rowKey = PoolRowKey.FromDieType(DieType.Damage);
            return true;
        }

        if (face.Armor > 0)
        {
            rowKey = PoolRowKey.FromDieType(DieType.Armor);
            return true;
        }

        if (face.TotalSelfDamageContribution > 0)
        {
            rowKey = PoolRowKey.FromDieType(DieType.Curse);
            return true;
        }

        for (var i = 0; i < face.ActionPoolContributions.Count; i++)
        {
            var extra = face.ActionPoolContributions[i];
            if (extra.Amount <= 0)
                continue;

            rowKey = extra.PoolKey;
            return true;
        }

        return false;
    }

    static bool TryGetPoolRowForDieType(FaceResult face, DieType filterType, out PoolRowKey rowKey)
    {
        rowKey = default;
        if (face == null)
            return false;

        switch (filterType)
        {
            case DieType.Damage:
                if (face.Type == DieType.Damage && face.TotalDamageContribution > 0)
                {
                    rowKey = PoolRowKey.FromDieType(DieType.Damage);
                    return true;
                }

                return false;
            case DieType.Armor:
                if (face.Armor > 0)
                {
                    rowKey = PoolRowKey.FromDieType(DieType.Armor);
                    return true;
                }

                return false;
            case DieType.Curse:
                if (face.TotalSelfDamageContribution > 0)
                {
                    rowKey = PoolRowKey.FromDieType(DieType.Curse);
                    return true;
                }

                return false;
            case DieType.Fire:
            case DieType.Ice:
            case DieType.Nature:
                if (face.Type == filterType && face.Damage > 0)
                {
                    rowKey = PoolRowKey.FromDieType(DieType.Damage);
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    static bool TryGetPoolRowForStatus(FaceResult face, StatusEffectSO statusEffect, out PoolRowKey rowKey)
    {
        rowKey = default;
        if (face == null || statusEffect == null)
            return false;

        var statusKey = PoolRowKey.Custom(statusEffect.name);
        for (var i = 0; i < face.ActionPoolContributions.Count; i++)
        {
            var extra = face.ActionPoolContributions[i];
            if (extra.Amount > 0 && extra.PoolKey.Equals(statusKey))
            {
                rowKey = statusKey;
                return true;
            }
        }

        return false;
    }
}
