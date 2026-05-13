using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One row of text/icon above a die, then flown to <see cref="StoredActionsPoolDisplay"/>.</summary>
public struct RollOutcomeVisualLine
{
    public PoolRowKey RowKey;
    public int Amount;
    /// <summary>Deferred action icon when set; otherwise catalog art from <see cref="PoolRowKey"/>.</summary>
    public Sprite IconOverride;
    /// <summary>True for immediate status rows used only for flyout; triggers delayed stored-pool resync after landing.</summary>
    public bool IsVisualFlyoutOnly;
}

/// <summary>Spawned when a die settles; flyouts target <see cref="StoredActionsPoolDisplay"/>.</summary>
public class DiceRollVisualPayload
{
    public Vector3 WorldAnchor;
    public Transform DieTransform;
    public List<RollOutcomeVisualLine> Lines;
    /// <summary>When true, visual controller defers this die until regular queued dice have completed.</summary>
    public bool ActivateAfterRegularDice;
    /// <summary>Hint from producer that this payload includes visual-only rows; final full pool resync runs when all queued die visuals finish.</summary>
    public bool NeedsDelayedStoredPoolResync;
    /// <summary>Reserved for custom visual handlers that need explicit full pool resync callback.</summary>
    public Action RequestFullStoredPoolResync;

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
    /// <summary>Totals for stored attack/defence plus deferred action rows (<see cref="FaceResult.ActionPoolContributions"/>).</summary>
    public static Action<Dictionary<PoolRowKey, int>> OnStoredActionsPoolUpdated;
    /// <summary>Force stored-actions pool UI to match combat (bust, reset). Skipped per roll when flyouts drive the bar.</summary>
    public static Action<Dictionary<PoolRowKey, int>> OnStoredActionsPoolIconsFullResync;
    /// <summary>3D die position + outcome lines; optional if no listener.</summary>
    public static Action<DiceRollVisualPayload> OnDiceRollVisualFeedback;

    /// <summary>Icons for actions that ran immediately on the rolled face (player HUD).</summary>
    public static Action<IReadOnlyList<Sprite>> OnImmediateGameActionIconsShown;
    /// <summary>Clear <see cref="OnImmediateGameActionIconsShown"/> UI (new combat turn).</summary>
    public static Action OnImmediateGameActionBarClear;
    /// <summary>Clear runtime icon overrides on the stored-actions pool.</summary>
    public static Action OnStoredActionsPoolRuntimeIconsClear;
    /// <summary>When a turn-end face resolves, set pool bar art for each element type that gained value (non-flyout mode).</summary>
    public static Action<PoolRowKey, Sprite> OnRuntimePoolIconForRow;

    // Interaction
    public static Action<DieAssetSO> OnDieToggled;
    public static Action OnRollCommand;
    public static Action OnEndTurnPressed;
    public static Action OnCheatWinPressed;
    public static Action OnCheatPerfectStrikePressed;

    // Check Battle Outcome
    public static Action OnPlayerVictory;
    public static Action OnPlayerDefeat;

    /// <summary>Player armor points destroyed by a single <see cref="PlayerDamageSource.EnemyPhysicalAttack"/> hit (not HP loss).</summary>
    public static Action<int> OnPlayerArmorLostToEnemyPhysicalAttack;

    /// <summary>Floating damage UI: amount hit for, world anchor (e.g. player).</summary>
    public static Action<int, Vector3> OnPlayerDamageNumber;
    /// <summary>Floating damage UI: amount, world anchor, which enemy was hit, and presentation channel (physical vs burn).</summary>
    public static Action<int, Vector3, EnemyController, EnemyDamagePresentationKind> OnEnemyDamagePresentation;
    /// <summary>Power orb reached enemy hit point or player support (HP) anchor; fires before turn physical damage from <see cref="CombatManager"/> applies.</summary>
    public static Action<PowerOrbImpactPayload> OnPowerOrbImpact;

    // Bust Logic
    public static Action<int, int> OnBustOccurred;
    /// <summary>Player confirmed bust resolution: all channeled element pool rows are cleared, then turn submits.</summary>
    public static Action OnBustResolved;

    // Rolls
    public static Action<int, int> OnRollsRemainingChanged; // (remaining, max)

    // Game State
    public static Action<CombatState> OnStateChanged;

    /// <summary>Fired when the player's turn begins again after the enemy turn (see <see cref="CombatManager"/> ResetTurn).</summary>
    public static Action OnPlayerTurnStarted;

    /// <summary>True while the reroll-die picker is active (block tray / use physics pick on dice).</summary>
    public static Action<bool> OnRerollDieSelectionModeChanged;

    /// <summary>
    /// Fired when <see cref="CombatManager"/> finishes combat setup for a session (deck, HP, turn state).
    /// Map runs with additive fight: fires each time a fight begins so UI (e.g. dice tray) can resync to <see cref="PlayerDataContainer.RuntimeData"/>.
    /// </summary>
    public static Action OnCombatSessionInitialized;
}