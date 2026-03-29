using System.Collections.Generic;
using UnityEngine;

public class StatusEffectManager : MonoBehaviour
{
    private readonly List<StatusEffectInstance> effects = new List<StatusEffectInstance>();

    public IReadOnlyList<StatusEffectInstance> Effects => effects;

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
    }

    public void RemoveStatus(StatusEffectSO definition, StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].Definition == definition)
            {
                effects[i].Definition.OnRemove(effects[i], ctx);
                effects.RemoveAt(i);
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

    public bool HasEffect<T>() where T : StatusEffectSO
    {
        foreach (var effect in effects)
            if (effect.Definition is T)
                return true;
        return false;
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
    }

    public void ClearAll(StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            effects[i].Definition.OnRemove(effects[i], ctx);
        }
        effects.Clear();
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

    private StatusEffectInstance FindInstance(StatusEffectSO definition)
    {
        foreach (var effect in effects)
            if (effect.Definition == definition)
                return effect;
        return null;
    }
}
