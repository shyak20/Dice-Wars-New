public enum CombatState
{
    WaitingForRoll,
    Rolling,
    BustCheck,
    TurnEnd,
    /// <summary>Banner / UI before enemy actions (see <see cref="CombatManager"/> enemy turn intro).</summary>
    EnemyTurnIntro,
    EnemyTurn,
    Victory, 
    Defeat
}