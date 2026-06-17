public enum CombatState
{
    WaitingForRoll,
    Rolling,
    BustCheck,
    /// <summary>Player must drag enemy-targeted rolled outcomes onto an enemy before the turn can continue (multi-enemy only).</summary>
    AwaitingTargetAssignment,
    TurnEnd,
    /// <summary>Banner / UI before enemy actions (see <see cref="CombatManager"/> enemy turn intro).</summary>
    EnemyTurnIntro,
    EnemyTurn,
    Victory, 
    Defeat
}