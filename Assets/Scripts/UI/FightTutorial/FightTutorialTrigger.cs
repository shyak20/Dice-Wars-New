/// <summary>Events that can start or finish a <see cref="FightTutorialPhase"/>.</summary>
public enum FightTutorialTrigger
{
    /// <summary>Unused / no gate.</summary>
    None = 0,

    /// <summary>
    /// Activate: show as soon as this phase is next in the list.
    /// Complete: not valid (use <see cref="AdvanceButton"/> or a player action).
    /// </summary>
    Immediate = 1,

    /// <summary><see cref="CombatEvents.OnCombatSessionInitialized"/>.</summary>
    CombatSessionReady = 2,

    /// <summary>Player clicked a die in the tray (<see cref="CombatEvents.OnDieToggled"/>).</summary>
    PlayerSelectDie = 3,

    /// <summary>First <see cref="CombatEvents.OnRollCommand"/> during this tutorial session.</summary>
    PlayerFirstRoll = 4,

    /// <summary>Assigned phase advance button was clicked.</summary>
    AdvanceButton = 5,

    /// <summary>
    /// Player pressed Roll (Roll button, Roll All, or Space) — any <see cref="CombatEvents.OnRollCommand"/>.
    /// Unlike <see cref="PlayerFirstRoll"/>, this fires on every roll press.
    /// </summary>
    RollButton = 6,

    /// <summary>
    /// Roll outcome flyouts finished for the current batch (<see cref="CombatEvents.OnRollResultsResolved"/>).
    /// Use to show a phase after results are on the pool / board, not when Roll is pressed.
    /// </summary>
    RollResultsResolved = 7,
}
