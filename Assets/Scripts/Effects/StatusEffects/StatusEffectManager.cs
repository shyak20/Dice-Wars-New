using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class StatusEffectManager : MonoBehaviour
{
    private readonly List<StatusEffectInstance> effects = new List<StatusEffectInstance>();

    CombatManager _boundCombatManager;
    PlayerStatus _boundPlayer;

    public IReadOnlyList<StatusEffectInstance> Effects => effects;

    public event Action OnEffectsChanged;
    private void NotifyChanged() => OnEffectsChanged?.Invoke();

    /// <summary>Lets burn-damage hooks build a <see cref="StatusEffectContext"/> from <see cref="EnemyController.TakeDamage"/>.</summary>
    public void BindBattleContext(CombatManager combatManager, PlayerStatus player)
    {
        _boundCombatManager = combatManager;
        _boundPlayer = player;
    }

    public StatusEffectContext CreateContextForEnemy(EnemyController enemy)
    {
        return new StatusEffectContext
        {
            CombatManager = _boundCombatManager,
            Player = _boundPlayer,
            Enemy = enemy
        };
    }

    public int ApplyBurnDamageModifiers(StatusEffectContext ctx, int damage)
    {
        foreach (var instance in effects)
            damage = instance.Definition.ModifyBurnDamageToOwner(instance, ctx, damage);
        return damage;
    }

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

    /// <summary>Sum of stacks from all <see cref="ThornsEffectSO"/> instances (retaliation damage).</summary>
    public int GetThornsRetaliateStacks()
    {
        var total = 0;
        foreach (var effect in effects)
        {
            if (effect.Definition is ThornsEffectSO)
                total += effect.Stacks;
        }

        return total;
    }

    public int GetStacks(StatusEffectSO definition)
    {
        var instance = FindInstance(definition);
        return instance?.Stacks ?? 0;
    }

    /// <summary>Multiplies stacks of the first effect of type T (first matching instance only).</summary>
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

    /// <summary>Multiplies every <see cref="BurnEffectSO"/> on this manager: each instance’s stacks become stacks × <paramref name="multiplier"/>.</summary>
    public void MultiplyAllBurnStacks(int multiplier, StatusEffectContext ctx)
    {
        if (multiplier <= 1) return;
        var changed = false;
        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect?.Definition is not BurnEffectSO) continue;
            if (effect.Stacks <= 0) continue;
            var delta = effect.Stacks * (multiplier - 1);
            if (delta <= 0) continue;
            effect.AddStacks(delta);
            changed = true;
        }

        if (changed)
            NotifyChanged();
    }

    /// <summary>Removes every <see cref="BurnEffectSO"/> (all stacks each). Returns total stacks removed.</summary>
    public int RemoveAllBurnStacks(StatusEffectContext ctx)
    {
        var total = 0;
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var inst = effects[i];
            if (inst?.Definition is not BurnEffectSO) continue;
            total += inst.Stacks;
            inst.Definition.OnRemove(inst, ctx);
            effects.RemoveAt(i);
        }

        if (total > 0)
            NotifyChanged();

        return total;
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

    /// <summary>
    /// Reduces stacks on one random debuff. If stacks reach 0, the debuff is removed.
    /// </summary>
    public bool ReduceRandomDebuffStacks(int stacksToRemove, StatusEffectContext ctx)
    {
        if (stacksToRemove <= 0)
            return false;

        var debuffIndices = new List<int>();
        for (var i = 0; i < effects.Count; i++)
        {
            if (effects[i].Definition.type == StatusEffectType.Debuff)
                debuffIndices.Add(i);
        }

        if (debuffIndices.Count == 0)
            return false;

        var index = debuffIndices[Random.Range(0, debuffIndices.Count)];
        var target = effects[index];
        var before = target.Stacks;
        target.RemoveStacks(stacksToRemove);
        var removed = target.IsExpired;

        if (removed)
        {
            target.Definition.OnRemove(target, ctx);
            effects.RemoveAt(index);
        }

        if (GameActionDebug.Enabled)
        {
            var removedAmount = Mathf.Min(before, stacksToRemove);
            Debug.Log(removed
                ? $"[StatusEffect] Cleansed {removedAmount} stack(s) from {target.Definition.effectName} (removed)"
                : $"[StatusEffect] Cleansed {removedAmount} stack(s) from {target.Definition.effectName} ({target.Stacks} remaining)");
        }

        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Reduces stacks on one random debuff that affects <paramref name="ownerTarget"/>.
    /// Burn is always treated as a cleanseable debuff for this target even if asset type is misconfigured.
    /// </summary>
    public bool ReduceRandomDebuffStacksForTarget(int stacksToRemove, StatusEffectContext ctx, StatusEffectTarget ownerTarget)
    {
        if (stacksToRemove <= 0)
            return false;

        var debuffIndices = new List<int>();
        for (var i = 0; i < effects.Count; i++)
        {
            var def = effects[i].Definition;
            if (def == null) continue;
            if (def.target != ownerTarget) continue;
            var isDebuff = def.type == StatusEffectType.Debuff || def is BurnEffectSO;
            if (isDebuff)
                debuffIndices.Add(i);
        }

        if (debuffIndices.Count == 0)
            return false;

        var index = debuffIndices[Random.Range(0, debuffIndices.Count)];
        var target = effects[index];
        var before = target.Stacks;
        target.RemoveStacks(stacksToRemove);
        var removed = target.IsExpired;

        if (removed)
        {
            target.Definition.OnRemove(target, ctx);
            effects.RemoveAt(index);
        }

        if (GameActionDebug.Enabled)
        {
            var removedAmount = Mathf.Min(before, stacksToRemove);
            Debug.Log(removed
                ? $"[StatusEffect] Cleansed {removedAmount} stack(s) from {target.Definition.effectName} (removed)"
                : $"[StatusEffect] Cleansed {removedAmount} stack(s) from {target.Definition.effectName} ({target.Stacks} remaining)");
        }

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

    /// <summary>
    /// Same resolution as <see cref="TickTurnStart"/> but waits between each <see cref="StatusEffectSO.OnTurnStart"/> (damage tick) so presentation can stagger.
    /// All <see cref="StatusEffectSO.OnTurnStart"/> calls still run before any stack decay, matching <see cref="TickTurnStart"/> ordering.
    /// </summary>
    public IEnumerator TickTurnStartStepped(
        StatusEffectContext ctx,
        float delaySecondsBeforeFirstOnTurnStart,
        float delaySecondsBetweenOnTurnStarts)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var instance = effects[i];

            if (instance.IsExpired)
            {
                instance.Definition.OnRemove(instance, ctx);
                effects.RemoveAt(i);
                if (GameActionDebug.Enabled)
                    Debug.Log($"[StatusEffect] {instance.Definition.effectName} expired (stacks reached 0)");
            }
        }

        var onTurnOrder = new List<StatusEffectInstance>();
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            if (!effects[i].IsExpired)
                onTurnOrder.Add(effects[i]);
        }

        for (var j = 0; j < onTurnOrder.Count; j++)
        {
            var instance = onTurnOrder[j];
            if (!effects.Contains(instance) || instance.IsExpired)
                continue;

            if (j == 0)
            {
                if (delaySecondsBeforeFirstOnTurnStart > 0f)
                    yield return new WaitForSeconds(delaySecondsBeforeFirstOnTurnStart);
            }
            else if (delaySecondsBetweenOnTurnStarts > 0f)
            {
                yield return new WaitForSeconds(delaySecondsBetweenOnTurnStarts);
            }

            if (!effects.Contains(instance) || instance.IsExpired)
                continue;

            instance.Definition.OnTurnStart(instance, ctx);
        }

        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var instance = effects[i];

            if (instance.Definition.stackDecayPerTurn > 0)
                instance.RemoveStacks(instance.Definition.stackDecayPerTurn);

            if (!instance.IsExpired) continue;

            instance.Definition.OnRemove(instance, ctx);
            effects.RemoveAt(i);
            if (GameActionDebug.Enabled)
                Debug.Log($"[StatusEffect] {instance.Definition.effectName} expired after stack decay (stacks reached 0)");
        }

        NotifyChanged();
    }

    /// <summary>
    /// Player debuffs that deal turn-start damage (e.g. enemy-applied Burn) tick here while last turn's armor is still up.
    /// Call before <see cref="PlayerStatus.ResetArmor"/> in <see cref="CombatManager.ResetTurn"/>.
    /// </summary>
    public void TickTurnStartBeforePlayerArmorReset(StatusEffectContext ctx)
        => TickTurnStartCore(ctx, TicksDamageBeforePlayerArmorReset);

    /// <summary>
    /// Per effect with stacks: <see cref="StatusEffectSO.OnTurnStart"/> runs at full stack count, then <see cref="StatusEffectSO.stackDecayPerTurn"/> is applied (e.g. burn damage = stacks, then decay).
    /// Skips player Burn/Poison already handled by <see cref="TickTurnStartBeforePlayerArmorReset"/>.
    /// </summary>
    public void TickTurnStart(StatusEffectContext ctx)
        => TickTurnStartCore(ctx, def => !TicksDamageBeforePlayerArmorReset(def));

    static bool TicksDamageBeforePlayerArmorReset(StatusEffectSO definition)
    {
        if (definition == null || definition.target != StatusEffectTarget.Player)
            return false;
        return definition is BurnEffectSO or PoisonEffectSO;
    }

    void TickTurnStartCore(StatusEffectContext ctx, System.Func<StatusEffectSO, bool> includeDefinition)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var instance = effects[i];

            if (instance.IsExpired)
            {
                instance.Definition.OnRemove(instance, ctx);
                effects.RemoveAt(i);
                if (GameActionDebug.Enabled)
                    Debug.Log($"[StatusEffect] {instance.Definition.effectName} expired (stacks reached 0)");
                continue;
            }

            if (!includeDefinition(instance.Definition))
                continue;

            instance.Definition.OnTurnStart(instance, ctx);
        }

        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var instance = effects[i];

            if (!includeDefinition(instance.Definition))
                continue;

            if (instance.Definition.stackDecayPerTurn > 0)
                instance.RemoveStacks(instance.Definition.stackDecayPerTurn);

            if (!instance.IsExpired) continue;

            instance.Definition.OnRemove(instance, ctx);
            effects.RemoveAt(i);
            if (GameActionDebug.Enabled)
                Debug.Log($"[StatusEffect] {instance.Definition.effectName} expired after stack decay (stacks reached 0)");
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

    /// <summary>True when the next roll batch would consume Echo and add no power (peek only).</summary>
    public bool WillEchoSkipPowerOnNextRollBatch()
    {
        for (var i = 0; i < effects.Count; i++)
        {
            if (effects[i].Definition is EchoEffectSO && effects[i].Stacks > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// If the player has <see cref="EchoEffectSO"/> stacks, removes one: the current roll batch (all dice) will not add to power.
    /// </summary>
    public bool TryConsumeEchoPowerSkipForNextRollBatch(StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            if (!(effects[i].Definition is EchoEffectSO))
                continue;
            var inst = effects[i];
            if (inst.Stacks <= 0)
                continue;
            inst.RemoveStacks(1);
            if (inst.IsExpired)
            {
                inst.Definition.OnRemove(inst, ctx);
                effects.RemoveAt(i);
            }

            if (GameActionDebug.Enabled)
                Debug.Log("[Echo status] Consumed 1 stack — dice in this roll batch will not increase power.");

            NotifyChanged();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes one stack of <see cref="ImmuneEffectSO"/> after an enemy hit that used Immune capping.
    /// </summary>
    public void ConsumeImmuneStackAfterHit(StatusEffectContext ctx)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            if (!(effects[i].Definition is ImmuneEffectSO))
                continue;
            var inst = effects[i];
            if (inst.Stacks <= 0)
                continue;
            inst.RemoveStacks(1);
            if (inst.IsExpired)
            {
                inst.Definition.OnRemove(inst, ctx);
                effects.RemoveAt(i);
            }

            if (GameActionDebug.Enabled)
                Debug.Log("[Immune status] Consumed 1 stack from the hit.");

            NotifyChanged();
            return;
        }
    }

    private StatusEffectInstance FindInstance(StatusEffectSO definition)
    {
        foreach (var effect in effects)
            if (effect.Definition == definition)
                return effect;
        return null;
    }
}
