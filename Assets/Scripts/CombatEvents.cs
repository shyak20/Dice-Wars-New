using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One row of text/icon shown above a die, then flown to the pool bar. Icon comes from <see cref="ElementPoolDisplay"/> for <see cref="Type"/>.</summary>
public struct RollOutcomeVisualLine
{
    public DieType Type;
    public int Amount;
}

/// <summary>Spawned by CombatManager when a die settles; UI flies lines to <see cref="ElementPoolDisplay"/>.</summary>
public class DiceRollVisualPayload
{
    public Vector3 WorldAnchor;
    public List<RollOutcomeVisualLine> Lines;

    Action _onVisualFinished;
    bool _visualFinishedReported;

    /// <summary>CombatManager registers this so bust / precision / turn flow waits for flyout.</summary>
    public void BindVisualFinished(Action onFinished) => _onVisualFinished = onFinished;

    /// <summary>Call exactly once when this die's flyout sequence ends (or is skipped).</summary>
    public void ReportVisualFinished()
    {
        if (_visualFinishedReported) return;
        _visualFinishedReported = true;
        _onVisualFinished?.Invoke();
    }
}

public static class CombatEvents
{
    // UI Updates
    public static Action<int, int> OnPowerChanged;
    public static Action<Dictionary<DieType, int>> OnPoolsUpdated;
    /// <summary>Forces element pool icons to match combat totals (bust, turn reset, overcharge). Not used per-die roll when flyout drives icons.</summary>
    public static Action<Dictionary<DieType, int>> OnPoolIconsFullResync;
    /// <summary>3D die position + outcome lines; optional if no listener.</summary>
    public static Action<DiceRollVisualPayload> OnDiceRollVisualFeedback;

    // Interaction
    public static Action<DieAssetSO> OnDieToggled;
    public static Action OnRollCommand;
    public static Action OnEndTurnPressed;

    // Check Battle Outcome
    public static Action OnPlayerVictory;
    public static Action OnPlayerDefeat;

    // Bust Logic
    public static Action<int, int> OnBustOccurred;
    public static Action<bool> OnBustResolved;

    // Rolls
    public static Action<int, int> OnRollsRemainingChanged; // (remaining, max)

    // Game State
    public static Action<CombatState> OnStateChanged;
}