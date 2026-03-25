using System;

public static class CombatEvents
{
    // UI Updates
    public static Action<int, int> OnPowerChanged;
    public static Action<int, int> OnPoolsUpdated;

    // Interaction
    public static Action<DieAssetSO> OnDieToggled;
    public static Action OnRollCommand;
    public static Action OnEndTurnPressed;

    // Bust Logic
    public static Action<int, int> OnBustOccurred;
    public static Action<bool> OnBustResolved;

    // Game State
    public static Action<CombatState> OnStateChanged;
}