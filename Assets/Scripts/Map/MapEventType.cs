/// <summary>Encounter / interactable type for a map cell (start and boss tiles use special cases in UI).</summary>
public enum MapEventType
{
    /// <summary>Start tile or empty walkway — no event when entered.</summary>
    None,
    CombatNormal,
    CombatElite,
    CombatBoss,
    Shop,
    Unknown,
    Shrine
}
