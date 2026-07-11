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
    /// <summary>Row frame behind the icon; when null, <see cref="StoredActionsPoolDisplay"/> uses <see cref="GameIconCatalog.TryGetPoolRowBackground"/>.</summary>
    public Sprite BackgroundOverride;
    /// <summary>True for immediate status rows used only for flyout; triggers delayed stored-pool resync after landing.</summary>
    public bool IsVisualFlyoutOnly;
    /// <summary>When true with <see cref="IsVisualFlyoutOnly"/>, fly to the player status bar instead of the element pool.</summary>
    public bool FlyToPlayerStatusBar;
    /// <summary>Multi-enemy: this row targets an enemy (physical/element damage or an enemy debuff) and must be dragged onto one when 2+ enemies are alive.</summary>
    public bool EnemyTargeted;
    /// <summary>Multi-enemy: the enemy-targeted action this row came from (Burn, Vulnerable, ...). Null means the row is the face's direct damage piece.</summary>
    public ApplyStatusEffectAction SourceAction;
    /// <summary>Multi-enemy: this piece resolves the instant it is dropped on an enemy (Trigger Immediately) instead of accumulating.</summary>
    public bool ResolvesImmediatelyOnDrop;
    /// <summary>When true with <see cref="EnemyTargeted"/>, duplicates fly to every alive enemy automatically after spawn motion.</summary>
    public bool AttackAllEnemies;
    /// <summary>Spawns with gather flyouts, stays above the die until die-to-die reroll launches, then duplicates per target.</summary>
    public bool ParkUntilDieToDieReroll;
    /// <summary>Increase Other source row: shows bonus above the keeper die, parked until die-to-die flyouts launch (does not fly to pool).</summary>
    public bool RemoveOnIncreaseOtherLaunch;
    /// <summary>When true, this row is one hit from a split <see cref="DieType.Damage"/> face (<see cref="FaceResult.DamageAttackTimes"/> &gt; 1).</summary>
    public bool IsSplitDamageHitLine;
    /// <summary>0-based hit index when <see cref="IsSplitDamageHitLine"/> is set.</summary>
    public int DamageHitIndex;
    /// <summary>When set, flies to this enemy's element container without drag assignment (already bound / legacy pre-assign).</summary>
    public EnemyController PreAssignedEnemy;
    /// <summary>Enemy-targeted pool extra (e.g. relic face-list damage): assigning updates that enemy's pool without changing the face's main damage target.</summary>
    public bool IsRelicPoolExtraLine;
    /// <summary>Self-damage / player-only deferred rows that must land on the shared player element container (never enemy drag tokens).</summary>
    public bool FlyToPlayerElementContainer;
    /// <summary>
    /// True when this row's amount is multiplied by Perfect Cast (face damage/armor/self, or a strike-scaled pool
    /// contribution). Enemy-pool deposits and drag tokens scale their displayed/assigned amount by this so the shown
    /// number matches the damage/status actually resolved. Mirrors <see cref="CombatManager"/>'s strike-scale rule.
    /// </summary>
    public bool PerfectStrikeScales;
    /// <summary>Relic or gem that granted this row; shown briefly on the element-value flyout.</summary>
    public Sprite SourceBuffIcon;
}

/// <summary>Spawned when a die settles; flyouts target <see cref="StoredActionsPoolDisplay"/>.</summary>
public class DiceRollVisualPayload
{
    public Vector3 WorldAnchor;
    public Transform DieTransform;
    public List<RollOutcomeVisualLine> Lines;
    /// <summary>Multi-enemy: the rolled face this payload came from, so enemy-targeted lines can be turned into a drag-to-assign token.</summary>
    public FaceResult SourceFace;
    /// <summary>When true, visual controller defers this die until regular queued dice have completed.</summary>
    public bool ActivateAfterRegularDice;
    /// <summary>Hint from producer that this payload includes visual-only rows; final full pool resync runs when all queued die visuals finish.</summary>
    public bool NeedsDelayedStoredPoolResync;
    /// <summary>Reserved for custom visual handlers that need explicit full pool resync callback.</summary>
    public Action RequestFullStoredPoolResync;

    Action _onVisualFinished;
    Action _onRaiseFinished;
    bool _visualFinishedReported;
    bool _raiseFinishedReported;

    /// <summary>CombatManager registers this so bust / precision / turn flow waits for flyout.</summary>
    public void BindVisualFinished(Action onFinished) => _onVisualFinished = onFinished;

    /// <summary>CombatManager registers this — fired after spawn/raise above the die, before FlyAB to the pool.</summary>
    public void BindRaiseFinished(Action onFinished) => _onRaiseFinished = onFinished;

    /// <summary>Call exactly once when this die's raise/stack sequence completes (before FlyAB).</summary>
    public void ReportRaiseFinished()
    {
        if (_raiseFinishedReported) return;
        _raiseFinishedReported = true;
        _onRaiseFinished?.Invoke();
    }

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

    /// <summary>
    /// When true, listeners must not overwrite pool amount text — Perfect Cast jackpot owns staggered value reveals.
    /// Set by <see cref="CombatManager"/> for the jackpot presentation window.
    /// </summary>
    public static bool DeferStoredActionsPoolIconFullResync { get; private set; }

    public static void SetDeferStoredActionsPoolIconFullResync(bool defer) => DeferStoredActionsPoolIconFullResync = defer;

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
    /// <summary>When a deferred action row resolves, set the row frame sprite from <see cref="GameIconIndexSO"/> (non-flyout mode).</summary>
    public static Action<PoolRowKey, Sprite> OnRuntimePoolRowBackgroundForRow;

    // Interaction
    public static Action<DieAssetSO> OnDieToggled;
    public static Action OnRollCommand;
    public static Action OnEndTurnPressed;
    public static Action OnCheatWinPressed;
    public static Action OnCheatPerfectStrikePressed;

    // Check Battle Outcome
    public static Action OnPlayerVictory;
    public static Action OnPlayerDefeat;

    /// <summary>Fired when <see cref="PlayerStatus"/> HP reaches 0 after damage. Combat listens and shows defeat.</summary>
    public static Action OnPlayerHealthDepleted;

    /// <summary>Player armor points destroyed by a single <see cref="PlayerDamageSource.EnemyPhysicalAttack"/> hit (not HP loss).</summary>
    public static Action<int> OnPlayerArmorLostToEnemyPhysicalAttack;

    /// <summary>Floating damage UI: amount hit for, world anchor (e.g. player).</summary>
    public static Action<int, Vector3> OnPlayerDamageNumber;
    /// <summary>Floating damage UI: amount, world anchor, which enemy was hit, and presentation channel (physical vs burn).</summary>
    public static Action<int, Vector3, EnemyController, EnemyDamagePresentationKind> OnEnemyDamagePresentation;
    /// <summary>Power orb reached enemy hit point or player support (HP) anchor; fires before turn physical damage from <see cref="CombatManager"/> applies.</summary>
    public static Action<PowerOrbImpactPayload> OnPowerOrbImpact;

    // Multi-enemy roster
    /// <summary>Fired when the active enemy roster changes (combat start, spawn-on-load, mid-combat spawn, or an enemy dies). Carries all currently-alive enemies.</summary>
    public static Action<System.Collections.Generic.IReadOnlyList<EnemyController>> OnEnemyRosterChanged;
    /// <summary>Fired when a new enemy becomes active in the roster (spawn-on-load or via a spawn action).</summary>
    public static Action<EnemyController> OnEnemySpawned;
    /// <summary>Fired once when an enemy reaches 0 HP and is removed from the active roster.</summary>
    public static Action<EnemyController> OnEnemyDefeated;

    // Target assignment (drag enemy-targeted outcomes onto an enemy)
    /// <summary>True while the player must assign rolled enemy-targeted outcomes to enemies (drag-and-drop). Roll / End Turn are blocked while true.</summary>
    public static Action<bool> OnTargetAssignmentModeChanged;
    /// <summary>True while unassigned drag tokens exist on the flyout canvas (includes spawn animation before assignment gate opens).</summary>
    public static Action<bool> OnRollOutcomeTokensPendingChanged;

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