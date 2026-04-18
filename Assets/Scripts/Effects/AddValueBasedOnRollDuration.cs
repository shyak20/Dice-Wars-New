/// <summary>How long <see cref="AddValueBasedOnRollAction"/> / <see cref="RelicAddValueBasedOnRollAction"/> keeps reacting to matching face values.</summary>
public enum AddValueBasedOnRollDuration
{
    /// <summary>Bonus applies only when this die resolves and its value matches <c>requiredFaceValue</c> (face Execute / relic modify hooks).</summary>
    SameRoll,

    /// <summary>
    /// When a die carrying this action resolves, registers a watcher for the rest of the player turn: every <b>later settled die</b>
    /// (including other dice in the same roll batch) that shows <c>requiredFaceValue</c> gains the bonus. The settling die that registered does not get this watcher bonus.
    /// Cleared after the enemy acts (next player turn re-registers Same Turn relics only via <see cref="RelicPhases.AfterEnemyTurnPlayerTurnStart"/>).
    /// </summary>
    SameTurn,

    /// <summary>Like <see cref="SameTurn"/> for per-die timing after registration, but persists for the whole combat.</summary>
    EntireCombat
}
