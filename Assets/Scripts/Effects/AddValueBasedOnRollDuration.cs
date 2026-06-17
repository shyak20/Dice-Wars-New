/// <summary>How long <see cref="AddValueBasedOnRollAction"/> / <see cref="RelicAddValueBasedOnRollAction"/> applies after activation.</summary>
public enum AddValueBasedOnRollDuration
{
    /// <summary>Grants <c>amount</c> on the resolving die when its value is in required face values.</summary>
    SameRoll,

    /// <summary>After activation, every later die this turn (any face value) gains <c>amount</c> via <c>bonusType</c>.</summary>
    SameTurn,

    /// <summary>Like <see cref="SameTurn"/>, but for the whole combat.</summary>
    EntireCombat
}
