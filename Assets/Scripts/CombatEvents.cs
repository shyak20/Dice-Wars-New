using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One row of text/icon shown above a die, then flown to the pool bar. Icon comes from <see cref="ElementPoolDisplay"/> for <see cref="Type"/>.</summary>
public struct RollOutcomeVisualLine
{
    public DieType Type;
    public int Amount;
    /// <summary>From turn-end actions on this face; flyout + pool use when set, else <see cref="ElementPoolDisplay"/> default art.</summary>
    public Sprite IconOverride;
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

    /// <summary>Icons for actions that ran immediately on the rolled face (player HUD).</summary>
    public static Action<IReadOnlyList<Sprite>> OnImmediateGameActionIconsShown;
    /// <summary>Clear <see cref="OnImmediateGameActionIconsShown"/> UI (new combat turn).</summary>
    public static Action OnImmediateGameActionBarClear;
    /// <summary>Clear dynamic pool icons from turn-end actions; default element art is shown again.</summary>
    public static Action OnElementPoolRuntimeIconsClear;
    /// <summary>When a turn-end face resolves, set pool bar art for each element type that gained value (non-flyout mode).</summary>
    public static Action<DieType, Sprite> OnRuntimePoolIconForType;

    // Interaction
    public static Action<DieAssetSO> OnDieToggled;
    public static Action OnRollCommand;
    public static Action OnEndTurnPressed;
    public static Action OnCheatWinPressed;

    // Check Battle Outcome
    public static Action OnPlayerVictory;
    public static Action OnPlayerDefeat;

    /// <summary>Floating damage UI: amount hit for, world anchor (e.g. player).</summary>
    public static Action<int, Vector3> OnPlayerDamageNumber;
    /// <summary>Floating damage UI: amount, world anchor, which enemy was hit (so only that prefab's UI spawns).</summary>
    public static Action<int, Vector3, EnemyController> OnEnemyDamageNumber;

    // Bust Logic
    public static Action<int, int> OnBustOccurred;
    public static Action<bool> OnBustResolved;

    // Rolls
    public static Action<int, int> OnRollsRemainingChanged; // (remaining, max)

    // Game State
    public static Action<CombatState> OnStateChanged;

    /// <summary>Fired when the player's turn begins again after the enemy turn (see <see cref="CombatManager"/> ResetTurn).</summary>
    public static Action OnPlayerTurnStarted;

    /// <summary>True while the reroll-die picker is active (block tray / use physics pick on dice).</summary>
    public static Action<bool> OnRerollDieSelectionModeChanged;
}