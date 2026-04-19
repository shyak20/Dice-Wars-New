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

    public static void BuildRows(EnemyActionSO intent, List<Row> into)
    {
        into.Clear();
        if (intent == null)
            return;

        if (intent.damage > 0)
        {
            var hits = Mathf.Max(1, intent.numberOfAttacks);
            var label = hits <= 1 ? intent.damage.ToString() : $"{intent.damage}x{hits}";
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
            default:
                return "";
        }
    }
}
