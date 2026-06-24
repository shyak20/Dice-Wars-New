using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>Builds UI rows for <see cref="EnemyActionSO"/> intent: damage, armor, then each game action.</summary>
public static class EnemyIntentSegments
{
    public readonly struct Row
    {
        public readonly Sprite Icon;
        public readonly Sprite Background;
        public readonly string ValueText;
        /// <summary>When set, hover uses <see cref="HoverTooltipManager.TryGetTooltipContent"/> (status definition).</summary>
        public readonly StatusEffectSO TooltipStatusEffect;
        public readonly string TooltipTitle;
        public readonly string TooltipDescription;
        public readonly bool EnableTooltip;

        public Row(Sprite icon, string valueText, StatusEffectSO tooltipStatusEffect, string tooltipTitle, string tooltipDescription, bool enableTooltip, Sprite background = null)
        {
            Icon = icon;
            Background = background;
            ValueText = valueText ?? "";
            TooltipStatusEffect = tooltipStatusEffect;
            TooltipTitle = tooltipTitle ?? "";
            TooltipDescription = tooltipDescription ?? "";
            EnableTooltip = enableTooltip;
        }
    }

    /// <param name="enemy">When set with <paramref name="combat"/>, physical strike text uses live damage (Strength, Chill, …).</param>
    /// <param name="buffDamageColor">TMP rich-text color for the per-hit number when it differs from <see cref="EnemyActionSO.damage"/>.</param>
    public static void BuildRows(EnemyActionSO intent, List<Row> into, EnemyController enemy = null, CombatManager combat = null, Color buffDamageColor = default)
    {
        into.Clear();
        if (intent == null)
            return;

        intent.MigrateLegacyActionDescription();

        if (intent.damage > 0)
        {
            var hits = Mathf.Max(1, intent.numberOfAttacks);
            var basePer = intent.damage;
            var computedPer = basePer;
            if (enemy != null && combat != null)
                computedPer = combat.PreviewEnemyPhysicalHitDamage(enemy, basePer);
            var label = FormatPhysicalIntentLabel(basePer, computedPer, hits, buffDamageColor);
            ResolveAttackIntentTooltip(intent, enemy, combat, out var attackTitle, out var attackDescription);
            var attackTooltip = HasTooltipContent(attackTitle, attackDescription);
            into.Add(new Row(
                GameIconCatalog.GetElementIcon(DieType.Damage),
                label,
                null,
                attackTitle,
                attackDescription,
                attackTooltip,
                GameIconCatalog.GetElementBackground(DieType.Damage)));
        }

        if (intent.armor > 0)
        {
            ResolveArmorIntentTooltip(intent, out var armorTitle, out var armorDescription);
            armorDescription = ApplyIntentArmorTooltipPlaceholders(armorDescription, intent);
            armorTitle = ApplyIntentArmorTooltipPlaceholders(armorTitle, intent);
            var armorTooltip = HasTooltipContent(armorTitle, armorDescription);
            into.Add(new Row(
                GameIconCatalog.GetElementIcon(DieType.Armor),
                intent.armor.ToString(),
                null,
                armorTitle,
                armorDescription,
                armorTooltip,
                GameIconCatalog.GetElementBackground(DieType.Armor)));
        }

        if (intent.actions == null)
            return;

        for (var i = 0; i < intent.actions.Count; i++)
        {
            var a = intent.actions[i];
            if (a == null || a is FaceResolveModifierBase)
                continue;

            var actionTypeName = a.GetType().FullName ?? a.GetType().Name;
            var icon = GameIconCatalog.GetEnemyActionIcon(actionTypeName) ?? GameActionIconUtility.GetDisplayIcon(a);
            var bg = GameIconCatalog.GetIntentActionBackground(a);

            if (a is ApplyStatusEffectAction apply && apply.StatusEffectDefinition != null)
            {
                if (TryResolveActionTooltip(intent, i, a, enemy, combat, out var statusTitle, out var statusDescription))
                {
                    into.Add(new Row(icon, DescribeActionAmount(a, enemy, combat, buffDamageColor), null, statusTitle, statusDescription, true, bg));
                    continue;
                }

                into.Add(new Row(icon, DescribeActionAmount(a, enemy, combat, buffDamageColor), apply.StatusEffectDefinition, null, null, true, bg));
                continue;
            }

            if (a is ApplyBenefitToMainEnemyAction benefitMain)
            {
                icon = benefitMain.ResolveIntentDisplayIcon() ?? icon;
                if (benefitMain.HasConfiguredArmor && !benefitMain.HasConfiguredHeal && !benefitMain.HasConfiguredStatus)
                    bg = GameIconCatalog.GetElementBackground(DieType.Armor);

                if (benefitMain.IsStatusOnlyBenefit && benefitMain.StatusEffectDefinition != null)
                {
                    if (TryResolveActionTooltip(intent, i, a, enemy, combat, out var statusTitle, out var statusDescription))
                    {
                        into.Add(new Row(icon, DescribeActionAmount(a, enemy, combat, buffDamageColor), null, statusTitle, statusDescription, true, bg));
                        continue;
                    }

                    into.Add(new Row(
                        icon,
                        DescribeActionAmount(a, enemy, combat, buffDamageColor),
                        benefitMain.StatusEffectDefinition,
                        null,
                        null,
                        true,
                        bg));
                    continue;
                }

                if (TryResolveActionTooltip(intent, i, a, enemy, combat, out var benefitTitle, out var benefitDescription))
                {
                    into.Add(new Row(icon, DescribeActionAmount(a, enemy, combat, buffDamageColor), null, benefitTitle, benefitDescription, true, bg));
                    continue;
                }

                ResolveNonStatusActionTooltip(intent, i, a, enemy, combat, out benefitTitle, out benefitDescription);
                into.Add(new Row(icon, DescribeActionAmount(a, enemy, combat, buffDamageColor), null, benefitTitle, benefitDescription, true, bg));
                continue;
            }

            if (TryResolveActionTooltip(intent, i, a, enemy, combat, out var tooltipTitle, out var tooltipDescription))
            {
                into.Add(new Row(icon, DescribeActionAmount(a, enemy, combat, buffDamageColor), null, tooltipTitle, tooltipDescription, true, bg));
                continue;
            }

            ResolveNonStatusActionTooltip(intent, i, a, enemy, combat, out tooltipTitle, out tooltipDescription);
            into.Add(new Row(icon, DescribeActionAmount(a, enemy, combat, buffDamageColor), null, tooltipTitle, tooltipDescription, true, bg));
        }
    }

    static bool HasTooltipContent(string title, string description) =>
        !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(description);

    static void ResolveAttackIntentTooltip(EnemyActionSO intent, EnemyController enemy, CombatManager combat, out string title, out string description)
    {
        title = string.IsNullOrWhiteSpace(intent.attackTooltipTitle)
            ? "<style=Attack>Attack</style>"
            : intent.attackTooltipTitle.Trim();
        description = intent.attackTooltipDescription?.Trim() ?? "";
        if (!TryGetAttackIntentTooltipAmount(intent, enemy, combat, out var amount))
            return;

        title = ApplyTooltipPlaceholder(title, amount);
        description = ApplyTooltipPlaceholder(description, amount);
    }

    static void ResolveArmorIntentTooltip(EnemyActionSO intent, out string title, out string description)
    {
        title = string.IsNullOrWhiteSpace(intent.armorTooltipTitle)
            ? "<style=Shield>Shield</style>"
            : intent.armorTooltipTitle.Trim();
        description = intent.armorTooltipDescription?.Trim() ?? "";
    }

    static bool TryResolveActionTooltip(
        EnemyActionSO intent,
        int actionIndex,
        IGameAction action,
        EnemyController enemy,
        CombatManager combat,
        out string title,
        out string description)
    {
        title = "";
        description = "";
        if (intent.actionTooltips == null || actionIndex >= intent.actionTooltips.Count || intent.actionTooltips[actionIndex] == null)
            return false;

        var entry = intent.actionTooltips[actionIndex];
        if (!string.IsNullOrWhiteSpace(entry.title))
            title = entry.title.Trim();
        if (!string.IsNullOrWhiteSpace(entry.description))
            description = entry.description.Trim();

        ApplyActionTooltipPlaceholders(ref title, ref description, action, enemy, combat);
        return HasTooltipContent(title, description);
    }

    static void ResolveNonStatusActionTooltip(
        EnemyActionSO intent,
        int actionIndex,
        IGameAction a,
        EnemyController enemy,
        CombatManager combat,
        out string title,
        out string description)
    {
        title = "Action";
        description = "";
        if (intent.actionTooltips != null && actionIndex < intent.actionTooltips.Count && intent.actionTooltips[actionIndex] != null)
        {
            var entry = intent.actionTooltips[actionIndex];
            if (!string.IsNullOrWhiteSpace(entry.title))
                title = entry.title.Trim();
            if (!string.IsNullOrWhiteSpace(entry.description))
                description = entry.description.Trim();
            ApplyActionTooltipPlaceholders(ref title, ref description, a, enemy, combat);
            return;
        }

        if (a is GameActionWithIcon gai)
        {
            var id = gai.GetActionVisualId();
            if (id != ActionVisualId.None && GameIconCatalog.TryGetActionTooltip(id, out var catalogTitle, out var catalogDesc))
            {
                if (!string.IsNullOrWhiteSpace(catalogTitle))
                    title = catalogTitle.Trim();
                else
                    title = id.ToString();
                if (!string.IsNullOrWhiteSpace(catalogDesc))
                    description = catalogDesc.Trim();
                ApplyActionTooltipPlaceholders(ref title, ref description, a, enemy, combat);
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(intent.actionName))
            title = intent.actionName.Trim();
    }

    static void ApplyActionTooltipPlaceholders(ref string title, ref string description, IGameAction action, EnemyController enemy, CombatManager combat)
    {
        if (!TryGetActionTooltipAmount(action, enemy, combat, out var amount))
            return;

        title = ApplyTooltipPlaceholder(title, amount);
        description = ApplyTooltipPlaceholder(description, amount);
    }

    static string ApplyTooltipPlaceholder(string text, int amount)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf("{0}", StringComparison.Ordinal) < 0)
            return text;

        return string.Format(text, amount);
    }

    static string ApplyIntentArmorTooltipPlaceholders(string text, EnemyActionSO intent)
    {
        if (intent == null || intent.armor <= 0)
            return text;

        return ApplyTooltipPlaceholder(text, intent.armor);
    }

    static bool TryGetAttackIntentTooltipAmount(EnemyActionSO intent, EnemyController enemy, CombatManager combat, out int amount)
    {
        amount = 0;
        if (intent == null || intent.damage <= 0)
            return false;

        amount = enemy != null && combat != null
            ? combat.PreviewEnemyPhysicalHitDamage(enemy, intent.damage)
            : intent.damage;
        return true;
    }

    static bool TryGetActionTooltipAmount(IGameAction action, EnemyController enemy, CombatManager combat, out int amount)
    {
        amount = 0;
        if (action == null)
            return false;

        switch (action)
        {
            case LeechPhysicalDamageAction leech when leech.Damage > 0:
                amount = enemy != null && combat != null
                    ? combat.PreviewEnemyPhysicalHitDamage(enemy, leech.Damage)
                    : leech.Damage;
                return true;
            case DealPlayerDamageOnSubmitAction deal when deal.Damage > 0:
                amount = deal.Damage;
                return true;
            case ApplyStatusEffectAction status when status.ConfiguredStacks > 0:
                amount = status.ConfiguredStacks;
                return true;
            case HealAction heal when heal.Amount > 0:
                amount = heal.Amount;
                return true;
            case MaxHpAction maxHp when maxHp.Amount > 0:
                amount = maxHp.Amount;
                return true;
            case ThornsAction thorns when thorns.ThornsPerHit > 0:
                amount = thorns.ThornsPerHit;
                return true;
            case OverchargeAction overcharge when overcharge.OverchargeAmount > 0:
                amount = overcharge.OverchargeAmount;
                return true;
            case CleanseAction cleanse when cleanse.CleanseStacks > 0:
                amount = cleanse.CleanseStacks;
                return true;
            case PrecisionAction precision when precision.PowerOfferAmount > 0:
                amount = precision.PowerOfferAmount;
                return true;
            case AddPowerAction addPower when addPower.PowerAmount > 0:
                amount = addPower.PowerAmount;
                return true;
            case ReducePlayerMaxPowerAction reduceMaxPower when reduceMaxPower.ReductionAmount > 0:
                amount = reduceMaxPower.ReductionAmount;
                return true;
            case IncreaseCombatMaxPowerAction increaseMaxPower when increaseMaxPower.Amount > 0:
                amount = increaseMaxPower.Amount;
                return true;
            case StartNextTurnWithArmorAction startArmor when startArmor.ArmorAmount > 0:
                amount = startArmor.ArmorAmount;
                return true;
            case BonusArmorBurnWhenEnemyHitsArmorAction struck when struck.BonusArmor > 0:
                amount = struck.BonusArmor;
                return true;
            case DamageFromEnemyBurnStacksPercentAction burnPct when burnPct.DamagePercentOfBurnStacks > 0:
                amount = PreviewPercentOfBurnStacks(enemy, burnPct.DamagePercentOfBurnStacks);
                return true;
            case InstantBurnDamageFromEnemyStacksPercentAction instantBurn when instantBurn.DealPercentOfBurnStacksAsDamage > 0:
                amount = PreviewPercentOfBurnStacks(enemy, instantBurn.DealPercentOfBurnStacksAsDamage);
                return true;
            case ArmorFromEnemyBurnStacksAction armorBurn when armorBurn.ArmorPercentOfBurnStacks > 0:
                amount = PreviewPercentOfBurnStacks(enemy, armorBurn.ArmorPercentOfBurnStacks);
                return true;
            case BonusDamageIfEnemyBurnMeetsThresholdAction burnThreshold when burnThreshold.BaseDamage > 0:
                amount = PreviewBonusDamageVsBurnThreshold(enemy, burnThreshold);
                return true;
            case ConsumeAllBurnForMaxHpAction burnToHp when burnToHp.StacksPerMaxHp > 0 && enemy != null:
                var burnStacks = SumBurnStacks(enemy);
                amount = burnStacks / burnToHp.StacksPerMaxHp;
                return true;
            case ApplyBenefitToMainEnemyAction benefitMain:
                if (benefitMain.HasConfiguredArmor)
                {
                    amount = benefitMain.ArmorAmount;
                    return true;
                }

                if (benefitMain.HasConfiguredHeal)
                {
                    amount = benefitMain.HealAmount;
                    return true;
                }

                if (benefitMain.HasConfiguredStatus)
                {
                    amount = benefitMain.StatusStacks;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    static int PreviewPercentOfBurnStacks(EnemyController enemy, int percent)
    {
        if (percent <= 0)
            return 0;

        var stacks = SumBurnStacks(enemy);
        return stacks * percent / 100;
    }

    static int PreviewBonusDamageVsBurnThreshold(EnemyController enemy, BonusDamageIfEnemyBurnMeetsThresholdAction action)
    {
        var stacks = SumBurnStacks(enemy);
        var damage = action.BaseDamage;
        if (stacks >= action.BurnStackThreshold && action.DamageMultiplierIfMet > 1)
            damage = action.BaseDamage * action.DamageMultiplierIfMet;
        return damage;
    }

    static int SumBurnStacks(EnemyController enemy)
    {
        var mgr = enemy?.StatusEffects;
        if (mgr == null)
            return 0;

        var effects = mgr.Effects;
        if (effects == null)
            return 0;

        var sum = 0;
        for (var i = 0; i < effects.Count; i++)
        {
            if (effects[i]?.Definition is BurnEffectSO)
                sum += effects[i].Stacks;
        }

        return sum;
    }

    static string DescribeActionAmount(IGameAction a, EnemyController enemy = null, CombatManager combat = null, Color buffDamageColor = default)
    {
        switch (a)
        {
            case ThornsAction t:
                return t.ThornsPerHit.ToString();
            case ApplyStatusEffectAction s:
                return s.ConfiguredStacks.ToString();
            case HealAction h:
                return h.Amount.ToString();
            case MaxHpAction m:
                return m.Amount.ToString();
            case OverchargeAction o:
                return o.OverchargeAmount.ToString();
            case CleanseAction c:
                return c.CleanseStacks.ToString();
            case PrecisionAction p:
                return p.PowerOfferAmount.ToString();
            case AddPowerAction ap:
                return ap.PowerAmount.ToString();
            case DamageFromEnemyBurnStacksPercentAction burnPct:
                return burnPct.DamagePercentOfBurnStacks <= 0 ? "" : $"{burnPct.DamagePercentOfBurnStacks}%";
            case InstantBurnDamageFromEnemyStacksPercentAction instantBurn:
                return instantBurn.DealPercentOfBurnStacksAsDamage <= 0 ? "" : $"{instantBurn.DealPercentOfBurnStacksAsDamage}%";
            case ConsumeAllBurnForMaxHpAction burnToHp:
                return burnToHp.StacksPerMaxHp <= 0 ? "" : $"/{burnToHp.StacksPerMaxHp}";
            case ArmorFromEnemyBurnStacksAction armorBurn:
                return armorBurn.ArmorPercentOfBurnStacks <= 0 ? "" : $"{armorBurn.ArmorPercentOfBurnStacks}%";
            case BonusArmorBurnWhenEnemyHitsArmorAction struck:
                return struck.BonusArmor <= 0 ? "" : $"{struck.BonusArmor}";
            case BonusDamageIfEnemyBurnMeetsThresholdAction burnDmg:
                return burnDmg.BaseDamage <= 0
                    ? ""
                    : $"{burnDmg.BaseDamage}/{burnDmg.BurnStackThreshold}×{burnDmg.DamageMultiplierIfMet}";
            case MultiplyPlayerBurnPoisonStacksAction mult:
                return $"x{Mathf.Max(2, mult.Multiplier)}";
            case ReducePlayerMaxPowerAction reduceMaxPower:
                return $"-{Mathf.Max(1, reduceMaxPower.ReductionAmount)}";
            case LeechPhysicalDamageAction leech:
                if (leech.Damage <= 0)
                    return "";
                var leechHits = Mathf.Max(1, leech.NumberOfAttacks);
                if (enemy != null && combat != null)
                {
                    var basePer = leech.Damage;
                    var computedPer = combat.PreviewEnemyPhysicalHitDamage(enemy, basePer);
                    return FormatPhysicalIntentLabel(basePer, computedPer, leechHits, buffDamageColor);
                }

                return leechHits <= 1 ? leech.Damage.ToString() : $"{leech.Damage}x{leechHits}";
            case ApplyBenefitToMainEnemyAction benefitMain:
                return benefitMain.FormatIntentAmountLabel();
            default:
                return "";
        }
    }

    static string FormatBuffedSingleHitLabel(int basePerHit, int computedPerHit, Color buffDamageColor)
    {
        if (computedPerHit == basePerHit)
            return computedPerHit.ToString();

        var c = buffDamageColor.a > 0.001f ? buffDamageColor : new Color(1f, 0.55f, 0.35f, 1f);
        var hex = ColorUtility.ToHtmlStringRGBA(c);
        return $"<color=#{hex}>{computedPerHit}</color>";
    }

    private static string FormatPhysicalIntentLabel(int basePerHit, int computedPerHit, int hits, Color buffDamageColor)
    {
        if (hits <= 1)
            return FormatBuffedSingleHitLabel(basePerHit, computedPerHit, buffDamageColor);

        if (computedPerHit == basePerHit)
            return $"{computedPerHit}x{hits}";

        var c = buffDamageColor.a > 0.001f ? buffDamageColor : new Color(1f, 0.55f, 0.35f, 1f);
        var hex = ColorUtility.ToHtmlStringRGBA(c);
        return $"<color=#{hex}>{computedPerHit}</color>x{hits}";
    }
}
