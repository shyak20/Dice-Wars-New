using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class StatusEffectManager : MonoBehaviour
{
    private readonly List<StatusEffectInstance> effects = new List<StatusEffectInstance>();

    public IReadOnlyList<StatusEffectInstance> Effects => effects;

    public event Action OnEffectsChanged;
    private void NotifyChanged() => OnEffectsChanged?.Invoke();

    public void ApplyStatus(StatusEffectSO definition, int stacks, StatusEffectContext ctx)
    {
        if (definition == null)
        {
            Debug.LogError("StatusEffectManager: Tried to apply null status effect!");
            return;
        }

        if (stacks <= 0)
        {
            Debug.LogError($"StatusEffectManager: Tried to apply {definition.effectName} with {stacks} stacks!");
            return;
        }

        var existing = FindInstance(definition);
        if (existing != null)
        {
            existing.AddStacks(stacks);
            definition.OnApply(existing, ctx);
            if (GameActionDebug.Enabled)
                Debug.Log($"[StatusEffect] {definition.effectName} +{stacks} stacks (total: {existing.Stacks})");
        }
        else
        {
            var instance = new StatusEffectInstance(definition, stacks);
            effects.Add(instance);
            definition.OnApply(instance, ctx);
            if (GameActionDebug.Enabled)
                Debug.Log($"[StatusEffect] {definition.effectName} applied with {stacks} stacks");
        }

        NotifyChanged();
    }

    public void RemoveStatus(StatusEffectSO definition, StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].Definition == definition)
            {
                effects[i].Definition.OnRemove(effects[i], ctx);
                effects.RemoveAt(i);
                NotifyChanged();
                return;
            }
        }
    }

    public void RemoveStatus<T>(StatusEffectContext ctx) where T : StatusEffectSO
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].Definition is T)
            {
                effects[i].Definition.OnRemove(effects[i], ctx);
                effects.RemoveAt(i);
                NotifyChanged();
                return;
            }
        }
    }

    public int GetStacks<T>() where T : StatusEffectSO
    {
        foreach (var effect in effects)
            if (effect.Definition is T)
                return effect.Stacks;
        return 0;
    }

    public int GetStacks(StatusEffectSO definition)
    {
        var instance = FindInstance(definition);
        return instance?.Stacks ?? 0;
    }

    /// <summary>Multiplies stacks of the first effect of type T (e.g. Burn ×2 for Fanning Flames).</summary>
    public void MultiplyStacks<T>(int multiplier, StatusEffectContext ctx) where T : StatusEffectSO
    {
        foreach (var effect in effects)
        {
            if (effect.Definition is T)
            {
                if (effect.Stacks <= 0 || multiplier <= 1) return;
                var delta = effect.Stacks * (multiplier - 1);
                if (delta > 0)
                    effect.AddStacks(delta);
                NotifyChanged();
                return;
            }
        }
    }

    public bool HasEffect<T>() where T : StatusEffectSO
    {
        foreach (var effect in effects)
            if (effect.Definition is T)
                return true;
        return false;
    }

    public bool RemoveRandomDebuff(StatusEffectContext ctx)
    {
        var debuffIndices = new List<int>();
        for (var i = 0; i < effects.Count; i++)
        {
            if (effects[i].Definition.type == StatusEffectType.Debuff)
                debuffIndices.Add(i);
        }

        if (debuffIndices.Count == 0)
            return false;

        var index = debuffIndices[Random.Range(0, debuffIndices.Count)];
        var removed = effects[index];
        removed.Definition.OnRemove(removed, ctx);
        effects.RemoveAt(index);

        if (GameActionDebug.Enabled)
            Debug.Log($"[StatusEffect] Cleansed {removed.Definition.effectName}");

        NotifyChanged();
        return true;
    }

    public void ClearDebuffs(StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].Definition.type == StatusEffectType.Debuff)
            {
                effects[i].Definition.OnRemove(effects[i], ctx);
                effects.RemoveAt(i);
            }
        }

        NotifyChanged();
    }

    public void ClearAll(StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            effects[i].Definition.OnRemove(effects[i], ctx);
        }
        effects.Clear();
        NotifyChanged();
    }

    public void TickTurnStart(StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var instance = effects[i];

            if (instance.Definition.stackDecayPerTurn > 0)
                instance.RemoveStacks(instance.Definition.stackDecayPerTurn);

            if (instance.IsExpired)
            {
                instance.Definition.OnRemove(instance, ctx);
                effects.RemoveAt(i);
                if (GameActionDebug.Enabled)
                    Debug.Log($"[StatusEffect] {instance.Definition.effectName} expired (stacks reached 0)");
                continue;
            }

            instance.Definition.OnTurnStart(instance, ctx);
        }

        NotifyChanged();
    }

    public void TickBeforeEnemyTurn(StatusEffectContext ctx)
    {
        foreach (var instance in effects)
            instance.Definition.OnBeforeEnemyTurn(instance, ctx);
    }

    public int ModifyEnemyHitDamage(StatusEffectContext ctx, int damage)
    {
        foreach (var instance in effects)
            damage = instance.Definition.ModifyEnemyHitDamage(instance, ctx, damage);
        return damage;
    }

    public void TickAfterEnemyTurn(StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            effects[i].Definition.OnAfterEnemyTurn(effects[i], ctx);

            if (effects[i].IsExpired)
            {
                effects[i].Definition.OnRemove(effects[i], ctx);
                if (GameActionDebug.Enabled)
                    Debug.Log($"[StatusEffect] {effects[i].Definition.effectName} expired (stacks reached 0)");
                effects.RemoveAt(i);
            }
        }

        NotifyChanged();
    }

    public void TickPerfectStrike(StatusEffectContext ctx)
    {
        foreach (var instance in effects)
            instance.Definition.OnPerfectStrike(instance, ctx);
    }

    public int ApplyDamageModifiers(StatusEffectContext ctx, int damage)
    {
        foreach (var instance in effects)
            damage = instance.Definition.ModifyDamageToOwner(instance, ctx, damage);
        return damage;
    }

    public int GetTotalBonusAttack(StatusEffectContext ctx)
    {
        var bonus = 0;
        foreach (var instance in effects)
            bonus += instance.Definition.GetBonusAttack(instance, ctx);
        return bonus;
    }

    public int GetTotalPerDieAttackDamageBonus(StatusEffectContext ctx)
    {
        var bonus = 0;
        foreach (var instance in effects)
            bonus += instance.Definition.GetPerDieAttackDamageBonus(instance, ctx);
        return bonus;
    }

    public int ModifyFaceValue(StatusEffectContext ctx, int value)
    {
        foreach (var instance in effects)
            value = instance.Definition.ModifyFaceValue(instance, ctx, value);
        return value;
    }

    public bool CheckRedirectAttackToSelf(StatusEffectContext ctx)
    {
        foreach (var instance in effects)
            if (instance.Definition.ShouldRedirectAttackToSelf(instance, ctx))
                return true;
        return false;
    }

    private StatusEffectInstance FindInstance(StatusEffectSO definition)
    {
        foreach (var effect in effects)
            if (effect.Definition == definition)
                return effect;
        return null;
    }
}
