using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds UI rows for <see cref="EnemyActionSO"/> intent: damage, armor, then each game action.</summary>
public static class EnemyIntentSegments
{
    public readonly struct Row
    {
        public readonly Sprite Icon;
        public readonly string ValueText;

        public Row(Sprite icon, string valueText)
        {
            Icon = icon;
            ValueText = valueText ?? "";
        }
    }

    /// <param name="enemy">When set with <paramref name="combat"/>, physical strike text uses live damage (Strength, Chill, …).</param>
    /// <param name="buffDamageColor">TMP rich-text color for the per-hit number when it differs from <see cref="EnemyActionSO.damage"/>.</param>
    public static void BuildRows(EnemyActionSO intent, List<Row> into, EnemyController enemy = null, CombatManager combat = null, Color buffDamageColor = default)
    {
        into.Clear();
        if (intent == null)
            return;

        if (intent.damage > 0)
        {
            var hits = Mathf.Max(1, intent.numberOfAttacks);
            var basePer = intent.damage;
            var computedPer = basePer;
            if (enemy != null && combat != null)
                computedPer = combat.PreviewEnemyPhysicalHitDamage(enemy, basePer);
            var label = FormatPhysicalIntentLabel(basePer, computedPer, hits, buffDamageColor);
            into.Add(new Row(GameIconCatalog.GetElementIcon(DieType.Damage), label));
        }

        if (intent.armor > 0)
            into.Add(new Row(GameIconCatalog.GetElementIcon(DieType.Armor), intent.armor.ToString()));

        if (intent.actions == null)
            return;

        foreach (var a in intent.actions)
        {
            if (a == null || a is FaceResolveModifierBase)
                continue;
            into.Add(new Row(GameActionIconUtility.GetDisplayIcon(a), DescribeActionAmount(a)));
        }
    }

    static string DescribeActionAmount(IGameAction a)
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
            default:
                return "";
        }
    }

    private static string FormatPhysicalIntentLabel(int basePerHit, int computedPerHit, int hits, Color buffDamageColor)
    {
        if (computedPerHit == basePerHit)
            return hits <= 1 ? computedPerHit.ToString() : $"{computedPerHit}x{hits}";

        var c = buffDamageColor.a > 0.001f ? buffDamageColor : new Color(1f, 0.55f, 0.35f, 1f);
        var hex = ColorUtility.ToHtmlStringRGBA(c);
        return hits <= 1
            ? $"<color=#{hex}>{computedPerHit}</color>"
            : $"<color=#{hex}>{computedPerHit}</color>x{hits}";
    }
}
