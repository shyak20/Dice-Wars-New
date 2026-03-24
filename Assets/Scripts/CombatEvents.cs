using System;
using System.Collections.Generic;

public static class CombatEvents
{
    public static Action<DieAssetSO> OnDieToggled; // Add/Remove from selection
    public static Action OnRollCommand;            // Triggered by the "Roll" button

    public static System.Action OnEndTurnPressed; // Add this line
    public static Action<int, int> OnPowerChanged;
    public static Action<CombatState> OnStateChanged;
    public static Action OnBustOccurred;
    public static Action<bool> OnBustResolved;
    public static Action<int, int> OnPoolsUpdated;
}