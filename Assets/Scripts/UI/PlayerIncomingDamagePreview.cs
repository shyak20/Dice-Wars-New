using System.Text;

/// <summary>
/// Predicted player HP loss from this turn's Thorns retaliation, the upcoming enemy round,
/// and the next Burn/Poison tick (current stacks and stacks enemies will apply this round).
/// </summary>
public readonly struct PlayerIncomingDamagePreview
{
    public readonly int TotalHpLoss;
    public readonly int ThornsHpLoss;
    public readonly int AttackHpLoss;
    public readonly int BurnHpLoss;
    public readonly int PoisonHpLoss;
    public readonly int AttackGross;
    public readonly int BurnStacks;
    public readonly int PoisonStacks;

    public PlayerIncomingDamagePreview(
        int totalHpLoss,
        int thornsHpLoss,
        int attackHpLoss,
        int burnHpLoss,
        int poisonHpLoss,
        int attackGross,
        int burnStacks,
        int poisonStacks)
    {
        TotalHpLoss = totalHpLoss;
        ThornsHpLoss = thornsHpLoss;
        AttackHpLoss = attackHpLoss;
        BurnHpLoss = burnHpLoss;
        PoisonHpLoss = poisonHpLoss;
        AttackGross = attackGross;
        BurnStacks = burnStacks;
        PoisonStacks = poisonStacks;
    }

    public string FormatTooltipDescription()
    {
        var sb = new StringBuilder(128);
        sb.Append("Total: ").Append(TotalHpLoss);
        if (ThornsHpLoss > 0)
            sb.Append("\nThorns: ").Append(ThornsHpLoss);
        if (AttackHpLoss > 0 || AttackGross > 0)
            sb.Append("\nAttacks: ").Append(AttackHpLoss);
        if (BurnStacks > 0)
            sb.Append("\nBurn: ").Append(BurnHpLoss);
        if (PoisonStacks > 0)
            sb.Append("\nPoison: ").Append(PoisonHpLoss);
        return sb.ToString();
    }
}
