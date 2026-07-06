using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns stacked outcome rows above a 3D die, then flies them into <see cref="StoredActionsPoolDisplay"/>.
/// </summary>
public class DiceRollOutcomeFlyoutController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform flyoutParent;
    [FormerlySerializedAs("elementPoolDisplay")]
    [SerializeField] private StoredActionsPoolDisplay storedActionsPoolDisplay;
    [SerializeField] private Camera worldCamera;
    [Tooltip("Optional. Status flyouts targeting the player fly here (player status bar).")]
    [SerializeField] private RectTransform playerStatusBarFlyTarget;
    [Tooltip("Optional. Status flyouts targeting the enemy fly here (enemy status bar).")]
    [SerializeField] private RectTransform enemyStatusBarFlyTarget;
    [Tooltip("Optional. Player status bar; frozen while status flyouts are in flight, then refreshed on landing.")]
    [SerializeField] private StatusEffectBarUI playerStatusBarUI;
    [Tooltip("Optional. Enemy status bar; frozen while status flyouts are in flight, then refreshed on landing.")]
    [SerializeField] private StatusEffectBarUI enemyStatusBarUI;

    [Header("Multi-enemy targeting")]
    [Tooltip("Optional. Combat manager used to detect multi-enemy fights (divert enemy-targeted outcomes to drag tokens).")]
    [SerializeField] private CombatManager combat;
    [Tooltip("Optional. Spawns drag-to-assign tokens for enemy-targeted outcomes when more than one enemy is alive. Falls back to the CombatManager's controller when left unassigned.")]
    [SerializeField] private RollTargetAssignmentController targetAssignment;

    /// <summary>The token controller to use, preferring the local reference and falling back to the CombatManager's.</summary>
    private RollTargetAssignmentController ResolveTargetAssignment()
    {
        if (targetAssignment != null)
            return targetAssignment;
        return combat != null ? combat.TargetAssignment : null;
    }

    [Header("Prefab")]
    [FormerlySerializedAs("linePrefab")]
    [SerializeField] private StoredActionsPoolIcon flyoutPoolIconPrefab;

    [Header("Layout")]
    [Tooltip("World-units: lift the stack anchor above the die’s projected position (uses DieTransform.position when set). Assign flyoutParent under the combat Canvas.")]
    [SerializeField] private float worldOffsetAboveDie = 0.75f;
    [Tooltip("Parent-local UI units: shift the projected die anchor along +X before stacking rows (tune if art doesn’t center on the 3D die).")]
    [SerializeField] private float layoutOffsetX;
    [Tooltip("Parent-local UI units: each additional line is stacked this far along +Y from the previous row.")]
    [SerializeField] private float lineSpacing = 36f;

    [Header("Spawn motion (above die)")]
    [Tooltip("How long spawn Y + scale curves run (scaled time). 0 = snap to curve end values.")]
    [SerializeField, Min(0f)] private float spawnAlongYDurationSeconds = 0.22f;
    [Tooltip("X = normalized time 0–1. Y = extra anchored Y in flyoutParent space (+Y = up). E.g. start positive, end 0 for rise-then-settle.")]
    [SerializeField] private AnimationCurve spawnYOffsetOverTime = new AnimationCurve(
        new Keyframe(0f, 28f), new Keyframe(1f, 0f));
    [Tooltip("X = normalized time 0–1. Y = uniform scale multiplier applied to the prefab root’s localScale (1 = final size).")]
    [SerializeField] private AnimationCurve spawnUniformScaleOverTime = new AnimationCurve(
        new Keyframe(0f, 0.92f), new Keyframe(1f, 1f));

    [Header("Timing")]
    [SerializeField] private float waitBeforeFlySeconds = 0.6f;
    [SerializeField] private float flyDurationSeconds = 0.45f;
    [SerializeField] private float delayBetweenDiceActivationsSeconds = 0.12f;

    [Header("Die activation feedback")]
    [Tooltip("Total duration of the RealToon Self Lit intensity pulse (up to 1, then back).")]
    [SerializeField] private float selfLitPulseDurationSeconds = 0.22f;
    [SerializeField] private float shakeDurationSeconds = 0.22f;
    [SerializeField] private float shakeAmplitude = 0.035f;

    [Header("Motion (A → B along quadratic bezier)")]
    [SerializeField] private AnimationCurve flyEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float arcHeightPixels = 96f;

    [Header("Drag interaction priority")]
    [Tooltip("Sorting order applied while drag tokens are on screen so this canvas wins raycasts over enemy UI.")]
    [SerializeField] private int interactionSortingOrder = 32767;
    [Tooltip("Switch to Screen Space Overlay while tokens are pending. Required when other Overlay canvases (e.g. enemy UI) would otherwise block pointer hits on Screen Space Camera canvases.")]
    [SerializeField] private bool useOverlayDuringDragInteraction = true;

    private readonly Queue<DiceRollVisualPayload> regularPayloadQueue = new Queue<DiceRollVisualPayload>();
    private readonly Queue<DiceRollVisualPayload> deferredPayloadQueue = new Queue<DiceRollVisualPayload>();
    private Coroutine queueRoutine;
    private int _playerStatusFreezeCount;
    private int _enemyStatusFreezeCount;
    private RenderMode _savedRenderMode;
    private int _savedSortingOrder;
    private float _savedPlaneDistance;
    private bool _interactionPriorityActive;

    struct ParkedRerollFlyoutEntry
    {
        public RectTransform ParkedRect;
        public DieToDieLaunchIcon LaunchIcon;
        public Vector2 AnchoredPosition;
    }

    readonly Dictionary<int, ParkedRerollFlyoutEntry> _parkedRerollByBatchGatherIndex = new Dictionary<int, ParkedRerollFlyoutEntry>();

    struct ActiveFlyoutLineEntry
    {
        public StoredActionsPoolIcon Icon;
        public int Amount;
    }

    readonly Dictionary<(int BatchGatherIndex, string RowStableId, int DamageHitIndex), ActiveFlyoutLineEntry> _activeFlyoutLinesByBatchRow =
        new Dictionary<(int, string, int), ActiveFlyoutLineEntry>();

    readonly Dictionary<int, RectTransform> _inFlightDieToDieActionIconByTargetBatch =
        new Dictionary<int, RectTransform>();

    struct ParkedIncreaseOtherFlyoutEntry
    {
        public RectTransform ParkedRect;
        public DieToDieLaunchIcon LaunchIcon;
        public Vector2 AnchoredPosition;
        public int BonusAmount;
    }

    readonly Dictionary<int, ParkedIncreaseOtherFlyoutEntry> _parkedIncreaseOtherByBatchGatherIndex =
        new Dictionary<int, ParkedIncreaseOtherFlyoutEntry>();

    public void ClearActiveFlyoutLineRegistry() => _activeFlyoutLinesByBatchRow.Clear();

    void ClearParkedIncreaseOtherFlyouts()
    {
        foreach (var entry in _parkedIncreaseOtherByBatchGatherIndex.Values)
        {
            if (entry.ParkedRect != null)
                Destroy(entry.ParkedRect.gameObject);
        }

        _parkedIncreaseOtherByBatchGatherIndex.Clear();
    }

    void ParkIncreaseOtherFlyoutUntilLaunch(
        FaceResult sourceFace,
        RollOutcomeVisualLine line,
        Vector2 anchoredPosition,
        RectTransform parkedRect)
    {
        if (sourceFace == null || sourceFace.BatchGatherIndex < 0 || parkedRect == null)
            return;

        var launchIcon = new DieToDieLaunchIcon(
            line.IconOverride,
            line.BackgroundOverride != null
                ? line.BackgroundOverride
                : GameIconCatalog.TryGetPoolRowBackground(line.RowKey));

        if (!launchIcon.HasAny)
            return;

        _parkedIncreaseOtherByBatchGatherIndex[sourceFace.BatchGatherIndex] = new ParkedIncreaseOtherFlyoutEntry
        {
            ParkedRect = parkedRect,
            LaunchIcon = launchIcon,
            AnchoredPosition = anchoredPosition,
            BonusAmount = line.Amount,
        };
    }

    void RemoveParkedIncreaseOtherSourceVisual(int sourceBatchGatherIndex)
    {
        if (sourceBatchGatherIndex < 0)
            return;

        if (!_parkedIncreaseOtherByBatchGatherIndex.TryGetValue(sourceBatchGatherIndex, out var parked))
            return;

        _parkedIncreaseOtherByBatchGatherIndex.Remove(sourceBatchGatherIndex);
        if (parked.ParkedRect == null)
            return;

        parked.ParkedRect.gameObject.SetActive(false);
        Destroy(parked.ParkedRect.gameObject);
    }

    public void TryDestroyInFlightDieToDieActionIcon(int targetBatchGatherIndex)
    {
        if (targetBatchGatherIndex < 0)
            return;

        if (!_inFlightDieToDieActionIconByTargetBatch.TryGetValue(targetBatchGatherIndex, out var rt))
            return;

        _inFlightDieToDieActionIconByTargetBatch.Remove(targetBatchGatherIndex);
        if (rt != null)
            Destroy(rt.gameObject);
    }

    void ClearInFlightDieToDieActionIcons()
    {
        foreach (var kvp in _inFlightDieToDieActionIconByTargetBatch)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }

        _inFlightDieToDieActionIconByTargetBatch.Clear();
    }

    public bool TryApplyFlyoutLineBonus(int batchGatherIndex, PoolRowKey rowKey, int bonusDelta, FaceResult face = null)
    {
        if (batchGatherIndex < 0 || bonusDelta <= 0)
            return false;

        var any = false;
        if (face != null && face.UsesSplitDamageHits && PoolRowKey.TryGetDieType(rowKey, out var dieType) && dieType == DieType.Damage)
        {
            var keysToUpdate = new List<(int BatchGatherIndex, string RowStableId, int DamageHitIndex)>();
            foreach (var kvp in _activeFlyoutLinesByBatchRow)
            {
                if (kvp.Key.BatchGatherIndex != batchGatherIndex || kvp.Key.RowStableId != rowKey.StableId || kvp.Key.DamageHitIndex < 0)
                    continue;

                keysToUpdate.Add(kvp.Key);
            }

            foreach (var key in keysToUpdate)
            {
                if (!_activeFlyoutLinesByBatchRow.TryGetValue(key, out var entry) || entry.Icon == null)
                    continue;

                entry.Amount += bonusDelta;
                entry.Icon.SetFlyoutAmountWithPulse(entry.Amount);
                _activeFlyoutLinesByBatchRow[key] = entry;
                any = true;
            }
        }
        else
        {
            var key = (batchGatherIndex, rowKey.StableId, -1);
            if (_activeFlyoutLinesByBatchRow.TryGetValue(key, out var entry) && entry.Icon != null)
            {
                entry.Amount += bonusDelta;
                entry.Icon.SetFlyoutAmountWithPulse(entry.Amount);
                _activeFlyoutLinesByBatchRow[key] = entry;
                any = true;
            }
        }

        if (!any)
        {
            var assignment = ResolveTargetAssignment();
            if (assignment != null && assignment.TryApplyPendingTokenBonus(batchGatherIndex, rowKey, bonusDelta, face))
                any = true;
        }

        return any;
    }

    public bool TryGetActiveFlyoutAmount(int batchGatherIndex, PoolRowKey rowKey, int damageHitIndex, out int amount)
    {
        amount = 0;
        if (batchGatherIndex < 0)
            return false;

        return _activeFlyoutLinesByBatchRow.TryGetValue(
            (batchGatherIndex, rowKey.StableId, damageHitIndex),
            out var entry) && (amount = entry.Amount) > 0;
    }

    void RegisterActiveFlyoutLine(int batchGatherIndex, PoolRowKey rowKey, int damageHitIndex, StoredActionsPoolIcon icon, int amount)
    {
        if (batchGatherIndex < 0 || icon == null || amount <= 0)
            return;

        _activeFlyoutLinesByBatchRow[(batchGatherIndex, rowKey.StableId, damageHitIndex)] = new ActiveFlyoutLineEntry
        {
            Icon = icon,
            Amount = amount,
        };
    }

    void UnregisterActiveFlyoutLines(int batchGatherIndex)
    {
        if (batchGatherIndex < 0)
            return;

        var removeKeys = new List<(int, string, int)>();
        foreach (var kvp in _activeFlyoutLinesByBatchRow)
        {
            if (kvp.Key.Item1 == batchGatherIndex)
                removeKeys.Add(kvp.Key);
        }

        foreach (var key in removeKeys)
            _activeFlyoutLinesByBatchRow.Remove(key);
    }

    public void ClearParkedRerollFlyouts()
    {
        foreach (var entry in _parkedRerollByBatchGatherIndex.Values)
        {
            if (entry.ParkedRect != null)
                Destroy(entry.ParkedRect.gameObject);
        }

        _parkedRerollByBatchGatherIndex.Clear();
        ClearActiveFlyoutLineRegistry();
        ClearInFlightDieToDieActionIcons();
        ClearParkedIncreaseOtherFlyouts();
    }

    public void ClearParkedRerollFlyout(int batchGatherIndex) =>
        _parkedRerollByBatchGatherIndex.Remove(batchGatherIndex);

    /// <summary>Removes the parked die-to-die action row above the source die without launching duplicates.</summary>
    public void ConsumeParkedDieToDieActionFlyout(int sourceBatchGatherIndex)
    {
        if (sourceBatchGatherIndex < 0)
            return;

        if (!_parkedRerollByBatchGatherIndex.TryGetValue(sourceBatchGatherIndex, out var parked))
            return;

        if (parked.ParkedRect != null)
            Destroy(parked.ParkedRect.gameObject);

        _parkedRerollByBatchGatherIndex.Remove(sourceBatchGatherIndex);
    }

    private void Awake()
    {
        if (canvas == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': canvas is not assigned!");
        if (flyoutParent == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': flyoutParent is not assigned!");
        if (storedActionsPoolDisplay == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': storedActionsPoolDisplay is not assigned!");
        if (flyoutPoolIconPrefab == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': flyoutPoolIconPrefab is not assigned!");
        if (worldCamera == null)
            worldCamera = Camera.main;
        if (spawnYOffsetOverTime == null || spawnYOffsetOverTime.length == 0)
            throw new System.InvalidOperationException("DiceRollOutcomeFlyoutController: spawnYOffsetOverTime must have at least one key.");

        ResolvePlayerStatusBarFlyTarget();

        // Tokens are created under this controller's canvas so they appear exactly where the flyout renders.
        var assignment = ResolveTargetAssignment();
        if (assignment != null && flyoutParent != null)
            assignment.SetTokenSpawnParent(flyoutParent);

        EnsureFlyoutGraphicRaycaster();
    }

    void ResolvePlayerStatusBarFlyTarget()
    {
        if (playerStatusBarUI == null)
            return;

        playerStatusBarUI.BringBarToFront();
        if (playerStatusBarUI.IconContainerRect != null)
            playerStatusBarFlyTarget = playerStatusBarUI.IconContainerRect;
    }

    private float EvaluateSpawnScaleMultiplier(float normalizedTime)
    {
        if (spawnUniformScaleOverTime == null || spawnUniformScaleOverTime.length == 0)
            return 1f;
        return spawnUniformScaleOverTime.Evaluate(normalizedTime);
    }

    private void OnEnable()
    {
        CombatEvents.OnDiceRollVisualFeedback += HandleRollVisual;
        CombatEvents.OnRollOutcomeTokensPendingChanged += HandleRollOutcomeTokensPendingChanged;
        CombatEvents.OnBustResolved += HandleBustResolved;
        ResolvePlayerStatusBarFlyTarget();
    }

    private void OnDisable()
    {
        CombatEvents.OnDiceRollVisualFeedback -= HandleRollVisual;
        CombatEvents.OnRollOutcomeTokensPendingChanged -= HandleRollOutcomeTokensPendingChanged;
        CombatEvents.OnBustResolved -= HandleBustResolved;
        if (queueRoutine != null)
        {
            StopCoroutine(queueRoutine);
            queueRoutine = null;
        }
        DrainQueueAndReportFinished(regularPayloadQueue);
        DrainQueueAndReportFinished(deferredPayloadQueue);
        ClearParkedRerollFlyouts();
        ForceUnfreezeStatusBars();
        ForceRestoreInteractionPriority();
    }

    private void HandleRollOutcomeTokensPendingChanged(bool pending)
    {
        SetFlyoutInteractionPriority(pending);
    }

    private void EnsureFlyoutGraphicRaycaster()
    {
        if (canvas == null)
            return;
        if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    /// <summary>
    /// Raises the flyout canvas above all combat UI for pointer hits while drag tokens are on screen.
    /// Screen Space Overlay wins over Screen Space Camera regardless of sorting order.
    /// </summary>
    private void SetFlyoutInteractionPriority(bool active)
    {
        if (canvas == null)
            return;

        if (active)
        {
            if (_interactionPriorityActive)
                return;

            _savedRenderMode = canvas.renderMode;
            _savedSortingOrder = canvas.sortingOrder;
            _savedPlaneDistance = canvas.planeDistance;
            canvas.overrideSorting = true;
            canvas.sortingOrder = interactionSortingOrder;
            if (useOverlayDuringDragInteraction)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            EnsureFlyoutGraphicRaycaster();
            _interactionPriorityActive = true;
            return;
        }

        if (!_interactionPriorityActive)
            return;

        canvas.renderMode = _savedRenderMode;
        canvas.sortingOrder = _savedSortingOrder;
        canvas.planeDistance = _savedPlaneDistance;
        _interactionPriorityActive = false;
    }

    private void ForceRestoreInteractionPriority()
    {
        if (!_interactionPriorityActive || canvas == null)
        {
            _interactionPriorityActive = false;
            return;
        }

        canvas.renderMode = _savedRenderMode;
        canvas.sortingOrder = _savedSortingOrder;
        canvas.planeDistance = _savedPlaneDistance;
        _interactionPriorityActive = false;
    }

    private bool PayloadHasTokenLines(DiceRollVisualPayload payload)
    {
        if (payload?.Lines == null)
            return false;

        for (var i = 0; i < payload.Lines.Count; i++)
        {
            if (ShouldDivertLineToToken(payload, payload.Lines[i]))
                return true;
        }

        return false;
    }

    private void HandleBustResolved() => DestroyTransientFlyoutChildren();

    private void DestroyTransientFlyoutChildren()
    {
        if (flyoutParent == null)
            return;

        for (var i = flyoutParent.childCount - 1; i >= 0; i--)
        {
            var child = flyoutParent.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private void HandleRollVisual(DiceRollVisualPayload payload)
    {
        if (payload == null)
        {
            Debug.LogError("DiceRollOutcomeFlyoutController: received null payload.");
            return;
        }

        if (payload.Lines == null || payload.Lines.Count == 0)
        {
            payload.ReportRaiseFinished();
            payload.ReportVisualFinished();
            TryNotifyFaceOutcomesSubmittedIfReady(payload);
            return;
        }

        if (canvas == null || flyoutParent == null || storedActionsPoolDisplay == null || flyoutPoolIconPrefab == null)
        {
            payload.ReportRaiseFinished();
            payload.ReportVisualFinished();
            return;
        }

        if (payload.ActivateAfterRegularDice)
            deferredPayloadQueue.Enqueue(payload);
        else
            regularPayloadQueue.Enqueue(payload);

        if (queueRoutine == null)
            queueRoutine = StartCoroutine(ProcessPayloadQueueRoutine());
    }

    private IEnumerator ProcessPayloadQueueRoutine()
    {
        try
        {
            while (regularPayloadQueue.Count > 0 || deferredPayloadQueue.Count > 0)
            {
                DiceRollVisualPayload payload;
                if (regularPayloadQueue.Count > 0)
                    payload = regularPayloadQueue.Dequeue();
                else
                    payload = deferredPayloadQueue.Dequeue();

                StartCoroutine(PlayFlyoutRoutine(payload));
                if (delayBetweenDiceActivationsSeconds > 0f)
                    yield return new WaitForSeconds(delayBetweenDiceActivationsSeconds);
            }
        }
        finally
        {
            queueRoutine = null;
        }
    }

    private static void SetLocalXY(RectTransform rt, Vector2 xy)
    {
        if (rt == null)
            return;
        rt.localPosition = new Vector3(xy.x, xy.y, 0f);
    }

    private IEnumerator PlayFlyoutRoutine(DiceRollVisualPayload payload)
    {
        var frozePlayerForFlyout = false;
        var frozeEnemyForFlyout = false;
        var lineRects = new List<RectTransform>();
        try
        {
            Vector3 dieWorld = payload.DieTransform != null ? payload.DieTransform.position : payload.WorldAnchor;

            if (PayloadHasTokenLines(payload))
                SetFlyoutInteractionPriority(true);

            Vector3 stackAnchorWorld = dieWorld + Vector3.up * worldOffsetAboveDie;
            if (!WorldPointToParentLocal(stackAnchorWorld, flyoutParent, out Vector2 anchorFlyoutLocal))
                yield break;

            TryBeginStatusBarFlyoutFreeze(payload, ref frozePlayerForFlyout, ref frozeEnemyForFlyout);

            Vector2 stackOriginFlyoutLocal = anchorFlyoutLocal + Vector2.right * layoutOffsetX;

            var flyLines = new List<RollOutcomeVisualLine>();
            var stackRestAnchored = new List<Vector2>();
            var spawnRootBaseScales = new List<Vector3>();
            var spawnRoutines = new List<Coroutine>();
            var spawnedTokenCount = 0;

            BeginEnemyTargetTokenPresentation(payload, stackAnchorWorld, spawnRoutines, ref spawnedTokenCount);

            if (payload.Lines != null)
            {
                for (var lineIndex = 0; lineIndex < payload.Lines.Count; lineIndex++)
                {
                    var line = payload.Lines[lineIndex];
                    if (ShouldDivertLineToToken(payload, line))
                        continue;

                    var icon = Instantiate(flyoutPoolIconPrefab, flyoutParent);
                    var rt = icon.transform as RectTransform;
                    if (rt == null)
                    {
                        Debug.LogError("DiceRollOutcomeFlyoutController: flyoutPoolIconPrefab root must have a RectTransform.");
                        Destroy(icon.gameObject);
                        continue;
                    }

                    var sprite = line.IconOverride != null ? line.IconOverride : storedActionsPoolDisplay.GetPoolRowSprite(line.RowKey);
                    if (line.ParkUntilDieToDieReroll)
                        icon.SetupForDieToDieActionFlyout(line.RowKey, sprite, line.BackgroundOverride);
                    else
                        icon.SetupForDiceRollFlyout(line.RowKey, sprite, line.Amount, line.BackgroundOverride);

                    if (!line.ParkUntilDieToDieReroll
                        && !line.RemoveOnIncreaseOtherLaunch
                        && line.Amount > 0
                        && payload.SourceFace != null)
                        RegisterActiveFlyoutLine(
                            payload.SourceFace.BatchGatherIndex,
                            line.RowKey,
                            line.IsSplitDamageHitLine ? line.DamageHitIndex : -1,
                            icon,
                            line.Amount);

                    Vector3 spawnBaseLocalScale = rt.localScale;
                    Vector2 basePos = stackOriginFlyoutLocal + Vector2.up * (lineIndex * lineSpacing);
                    Vector2 restAnchored = basePos + Vector2.up * spawnYOffsetOverTime.Evaluate(1f);
                    bool animateSpawn = spawnAlongYDurationSeconds > 1e-4f;
                    if (!animateSpawn)
                    {
                        SetLocalXY(rt, restAnchored);
                        rt.localScale = spawnBaseLocalScale * EvaluateSpawnScaleMultiplier(1f);
                    }
                    else
                    {
                        SetLocalXY(rt, basePos + Vector2.up * spawnYOffsetOverTime.Evaluate(0f));
                        rt.localScale = spawnBaseLocalScale * EvaluateSpawnScaleMultiplier(0f);
                        spawnRoutines.Add(StartCoroutine(CoSpawnPresentationMotion(rt, basePos, spawnBaseLocalScale)));
                    }

                    lineRects.Add(rt);
                    flyLines.Add(line);
                    stackRestAnchored.Add(restAnchored);
                    spawnRootBaseScales.Add(spawnBaseLocalScale);
                }
            }

            foreach (var c in spawnRoutines)
            {
                if (c != null)
                    yield return c;
            }

            if (lineRects.Count == 0 && spawnedTokenCount == 0)
                yield break;

            if (payload.DieTransform != null)
                yield return PlayDieActivationFeedback(payload.DieTransform);

            float spawnScaleEnd = EvaluateSpawnScaleMultiplier(1f);
            for (int s = 0; s < lineRects.Count; s++)
            {
                var rt = lineRects[s];
                if (rt == null) continue;
                SetLocalXY(rt, stackRestAnchored[s]);
                rt.localScale = spawnRootBaseScales[s] * spawnScaleEnd;
            }

            // Park before the fly gate so Increase Other can consume this row when duplicates launch
            // (that phase runs after raises finish but before flyouts fly to the pool).
            for (var i = 0; i < flyLines.Count && i < lineRects.Count; i++)
            {
                if (flyLines[i].RemoveOnIncreaseOtherLaunch && lineRects[i] != null)
                    ParkIncreaseOtherFlyoutUntilLaunch(payload?.SourceFace, flyLines[i], stackRestAnchored[i], lineRects[i]);
            }

            payload.ReportRaiseFinished();

            if (combat != null)
                yield return new WaitUntil(() => combat.IsFlyoutFlyPhaseAllowed);

            if (combat != null && combat.SkipFlyoutFlyPhaseThisBatch)
                yield break;

            if (lineRects.Count == 0)
                yield break;

            if (waitBeforeFlySeconds > 0f)
                yield return new WaitForSeconds(waitBeforeFlySeconds);

            var batchGatherIndex = payload.SourceFace != null ? payload.SourceFace.BatchGatherIndex : -1;
            for (var syncIndex = 0; syncIndex < flyLines.Count; syncIndex++)
            {
                var syncedLine = flyLines[syncIndex];
                var flyoutHitIndex = syncedLine.IsSplitDamageHitLine ? syncedLine.DamageHitIndex : -1;
                if (TryGetActiveFlyoutAmount(batchGatherIndex, syncedLine.RowKey, flyoutHitIndex, out var syncedAmount))
                {
                    syncedLine.Amount = syncedAmount;
                    flyLines[syncIndex] = syncedLine;
                }
            }

            for (int s = 0; s < lineRects.Count; s++)
            {
                var rt = lineRects[s];
                if (rt == null) continue;
                SetLocalXY(rt, stackRestAnchored[s]);
                rt.localScale = spawnRootBaseScales[s] * spawnScaleEnd;
            }

            var flyCoroutines = new List<Coroutine>();
            for (int i = 0; i < flyLines.Count && i < lineRects.Count; i++)
            {
                var line = flyLines[i];
                if (line.ParkUntilDieToDieReroll)
                {
                    ParkRerollFlyoutUntilLaunch(payload?.SourceFace, line, stackRestAnchored[i], lineRects[i]);
                    continue;
                }

                if (line.RemoveOnIncreaseOtherLaunch)
                    continue;

                if (lineRects[i] == null)
                    continue;

                if (line.AttackAllEnemies && line.EnemyTargeted)
                {
                    flyCoroutines.Add(StartCoroutine(FlyAttackAllLineToEnemies(
                        payload,
                        line,
                        lineRects[i],
                        stackRestAnchored[i])));
                    continue;
                }

                if (line.PreAssignedEnemy != null && line.PreAssignedEnemy.IsAlive)
                {
                    var assignedTarget = ResolveEnemyFlyTargetRect(line.PreAssignedEnemy, line, out var assignedIsEnemyOwnPool);
                    if (assignedTarget == null ||
                        !UiRectCenterToParentLocal(assignedTarget, flyoutParent, out Vector2 assignedEnd))
                    {
                        Destroy(lineRects[i].gameObject);
                        continue;
                    }

                    Vector2 assignedStart = stackRestAnchored[i];
                    Vector2 assignedMid = (assignedStart + assignedEnd) * 0.5f + Vector2.up * arcHeightPixels;
                    TryBeginDieDissolveForPayload(payload);
                    flyCoroutines.Add(StartCoroutine(FlyLineToEnemyAssignRoutine(
                        payload, lineRects[i], assignedStart, assignedMid, assignedEnd, line, line.PreAssignedEnemy,
                        applyToSharedPool: !assignedIsEnemyOwnPool)));
                    continue;
                }

                // Single living enemy: enemy-targeted attacks/debuffs fly into that enemy's element container (and assign to it),
                // rather than the shared player pool. Falls back to the player pool when the enemy has no element container wired.
                if (line.EnemyTargeted && payload?.SourceFace != null && TryResolveSoloFlyEnemy(out var soloEnemy))
                {
                    var soloTarget = ResolveEnemyFlyTargetRect(soloEnemy, line, out var soloIsEnemyOwnPool);
                    if (soloTarget == null ||
                        !UiRectCenterToParentLocal(soloTarget, flyoutParent, out Vector2 soloEnd))
                    {
                        Destroy(lineRects[i].gameObject);
                        continue;
                    }

                    Vector2 soloStart = stackRestAnchored[i];
                    Vector2 soloMid = (soloStart + soloEnd) * 0.5f + Vector2.up * arcHeightPixels;
                    TryBeginDieDissolveForPayload(payload);
                    flyCoroutines.Add(StartCoroutine(FlyLineToEnemyAssignRoutine(
                        payload, lineRects[i], soloStart, soloMid, soloEnd, line, soloEnemy,
                        applyToSharedPool: !soloIsEnemyOwnPool)));
                    continue;
                }

                RectTransform target = ResolveFlyTargetRect(line);
                if (target == null)
                {
                    Destroy(lineRects[i].gameObject);
                    continue;
                }

                if (!UiRectCenterToParentLocal(target, flyoutParent, out Vector2 endLocal))
                {
                    Destroy(lineRects[i].gameObject);
                    continue;
                }

                Vector2 startLocal = stackRestAnchored[i];
                Vector2 mid = (startLocal + endLocal) * 0.5f + Vector2.up * arcHeightPixels;
                var statusTarget = ResolveStatusTarget(line);
                var applyPoolDelta = ShouldApplyPoolDelta(line, statusTarget);
                if (applyPoolDelta)
                    TryBeginDieDissolveForPayload(payload);
                flyCoroutines.Add(StartCoroutine(FlyLineRoutine(lineRects[i], startLocal, mid, endLocal, line, applyPoolDelta)));
            }

            foreach (var c in flyCoroutines)
            {
                if (c != null)
                    yield return c;
            }
        }
        finally
        {
            if (frozePlayerForFlyout)
                EndFreezeStatusBar(StatusEffectTarget.Player);
            if (frozeEnemyForFlyout)
                EndFreezeStatusBar(StatusEffectTarget.Enemy);
            UnregisterActiveFlyoutLines(payload?.SourceFace != null ? payload.SourceFace.BatchGatherIndex : -1);
            TryNotifyFaceOutcomesSubmittedIfReady(payload);
            payload.ReportRaiseFinished();
            payload.ReportVisualFinished();
        }
    }

    /// <summary>
    /// Call synchronously at the start of a flyout (no yields before this) so status bar stack text
    /// does not refresh until <see cref="EndFreezeStatusBar"/> runs after lines land.
    /// </summary>
    private void TryBeginStatusBarFlyoutFreeze(DiceRollVisualPayload payload, ref bool frozePlayer, ref bool frozeEnemy)
    {
        if (payload?.Lines == null) return;

        var needPlayer = false;
        var needEnemy = false;
        for (var i = 0; i < payload.Lines.Count; i++)
        {
            var line = payload.Lines[i];
            var st = ResolveStatusTarget(line);
            if (!ShouldRouteStatusToStatusBar(line, st))
                continue;
            if (line.FlyToPlayerStatusBar || st == StatusEffectTarget.Player)
                needPlayer = true;
            else if (st == StatusEffectTarget.Enemy)
                needEnemy = true;
        }

        if (needPlayer && playerStatusBarUI != null)
        {
            BeginFreezeStatusBar(StatusEffectTarget.Player);
            frozePlayer = true;
        }

        if (needEnemy && enemyStatusBarUI != null)
        {
            BeginFreezeStatusBar(StatusEffectTarget.Enemy);
            frozeEnemy = true;
        }
    }

    private IEnumerator CoSpawnPresentationMotion(RectTransform rt, Vector2 baseAnchored, Vector3 prefabRootBaseScale)
    {
        float dur = spawnAlongYDurationSeconds;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float y = spawnYOffsetOverTime.Evaluate(u);
            SetLocalXY(rt, baseAnchored + Vector2.up * y);
            rt.localScale = prefabRootBaseScale * EvaluateSpawnScaleMultiplier(u);
            yield return null;
        }

        if (rt == null)
            yield break;

        SetLocalXY(rt, baseAnchored + Vector2.up * spawnYOffsetOverTime.Evaluate(1f));
        rt.localScale = prefabRootBaseScale * EvaluateSpawnScaleMultiplier(1f);
    }

    private IEnumerator CoSpawnTokenPresentationMotion(RolledOutcomeToken token, RectTransform rt, Vector2 baseAnchored,
        Vector3 prefabRootBaseScale, Vector2 restAnchored)
    {
        yield return CoSpawnPresentationMotion(rt, baseAnchored, prefabRootBaseScale);
        if (token == null || rt == null)
            yield break;

        SetLocalXY(rt, restAnchored);
        rt.localScale = prefabRootBaseScale * EvaluateSpawnScaleMultiplier(1f);
        token.SetDragEnabled(true);
    }

    private void BeginEnemyTargetTokenPresentation(DiceRollVisualPayload payload, Vector3 stackAnchorWorld,
        List<Coroutine> spawnRoutines, ref int spawnedTokenCount)
    {
        if (payload?.Lines == null || payload.Lines.Count == 0 || payload.SourceFace == null)
            return;

        var assignment = ResolveTargetAssignment();
        if (assignment == null)
            return;

        var tokenParent = assignment.TokenSpawnParent;
        if (tokenParent == null)
            return;

        if (!WorldPointToParentLocal(stackAnchorWorld, tokenParent, out var stackOriginLocal))
            return;
        stackOriginLocal += Vector2.right * layoutOffsetX;

        bool animateSpawn = spawnAlongYDurationSeconds > 1e-4f;
        for (var lineIndex = 0; lineIndex < payload.Lines.Count; lineIndex++)
        {
            var line = payload.Lines[lineIndex];
            if (!ShouldDivertLineToToken(payload, line))
                continue;

            var token = assignment.SpawnToken(payload.SourceFace, line, line.SourceAction, lineIndex, payload.Lines.Count,
                dragEnabled: false);
            if (token == null)
                continue;

            spawnedTokenCount++;
            var rt = token.RectTransform;
            var basePos = stackOriginLocal + Vector2.up * (lineIndex * lineSpacing);
            var restAnchored = basePos + Vector2.up * spawnYOffsetOverTime.Evaluate(1f);
            var spawnBaseLocalScale = rt.localScale;

            if (!animateSpawn)
            {
                SetLocalXY(rt, restAnchored);
                rt.localScale = spawnBaseLocalScale * EvaluateSpawnScaleMultiplier(1f);
                token.SetDragEnabled(true);
            }
            else
            {
                SetLocalXY(rt, basePos + Vector2.up * spawnYOffsetOverTime.Evaluate(0f));
                rt.localScale = spawnBaseLocalScale * EvaluateSpawnScaleMultiplier(0f);
                spawnRoutines.Add(StartCoroutine(CoSpawnTokenPresentationMotion(token, rt, basePos, spawnBaseLocalScale, restAnchored)));
            }
        }
    }

    private bool ShouldDivertLineToToken(DiceRollVisualPayload payload, RollOutcomeVisualLine line)
    {
        if (line.PreAssignedEnemy != null)
            return false;
        if (line.AttackAllEnemies)
            return false;
        if (payload?.SourceFace == null || !line.EnemyTargeted)
            return false;
        if (combat == null || !combat.IsMultiEnemy)
            return false;
        return ResolveTargetAssignment() != null;
    }

    private IEnumerator FlyLineRoutine(RectTransform rt, Vector2 start, Vector2 mid, Vector2 end, RollOutcomeVisualLine line, bool applyPoolDeltaOnLanding)
    {
        if (rt == null)
            yield break;

        float dur = Mathf.Max(0.01f, flyDurationSeconds);
        float t = 0f;
        while (t < dur)
        {
            if (rt == null)
                yield break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = flyEase != null ? flyEase.Evaluate(u) : u;
            SetLocalXY(rt, QuadraticBezier(start, mid, end, e));
            yield return null;
        }

        if (rt == null)
            yield break;

        SetLocalXY(rt, end);
        if (applyPoolDeltaOnLanding && storedActionsPoolDisplay != null && storedActionsPoolDisplay.UsesFlyoutIncrementMode)
            storedActionsPoolDisplay.ApplyPoolDelta(line.RowKey, line.Amount, line.IconOverride, line.BackgroundOverride);
        Destroy(rt.gameObject);
    }

    private IEnumerator FlyAttackAllLineToEnemies(
        DiceRollVisualPayload payload,
        RollOutcomeVisualLine line,
        RectTransform templateRt,
        Vector2 startAnchored)
    {
        if (templateRt == null)
            yield break;

        if (combat == null)
        {
            Debug.LogError("DiceRollOutcomeFlyoutController: 'combat' reference is not assigned, so Attack All Enemies faces cannot fly to enemies. Assign the CombatManager on the flyout controller.", this);
            yield break;
        }

        var enemies = CollectAliveEnemies();
        if (enemies.Count == 0)
        {
            Destroy(templateRt.gameObject);
            yield break;
        }

        TryBeginDieDissolveForPayload(payload);

        var assignRoutines = new List<Coroutine>();
        for (var e = 0; e < enemies.Count; e++)
        {
            RectTransform rt;
            if (e == 0)
            {
                rt = templateRt;
            }
            else
            {
                var icon = Instantiate(flyoutPoolIconPrefab, flyoutParent);
                rt = icon.transform as RectTransform;
                if (rt == null)
                {
                    Destroy(icon.gameObject);
                    continue;
                }

                var sprite = line.IconOverride != null ? line.IconOverride : storedActionsPoolDisplay.GetPoolRowSprite(line.RowKey);
                icon.SetupForDiceRollFlyout(line.RowKey, sprite, line.Amount, line.BackgroundOverride);
                SetLocalXY(rt, startAnchored);
                rt.localScale = templateRt.localScale;
            }

            var enemy = enemies[e];
            var targetRect = ResolveEnemyFlyTargetRect(enemy, line, out var isEnemyOwnPool);
            if (targetRect == null)
            {
                Debug.LogError(
                    $"DiceRollOutcomeFlyoutController: Attack All Enemies face has no fly target for enemy '{enemy.name}'. " +
                    "Assign the enemy's AssignedElementPool / DropTarget, or a shared StoredActionsPoolDisplay on the flyout controller.",
                    enemy);
                if (e > 0 && rt != null)
                    Destroy(rt.gameObject);
                continue;
            }

            if (!UiRectCenterToParentLocal(targetRect, flyoutParent, out var endLocal))
            {
                if (e > 0 && rt != null)
                    Destroy(rt.gameObject);
                continue;
            }

            var mid = (startAnchored + endLocal) * 0.5f + Vector2.up * arcHeightPixels;
            assignRoutines.Add(StartCoroutine(FlyLineToEnemyAssignRoutine(
                payload, rt, startAnchored, mid, endLocal, line, enemy, applyToSharedPool: !isEnemyOwnPool)));
        }

        foreach (var c in assignRoutines)
        {
            if (c != null)
                yield return c;
        }

        if (line.ResolvesImmediatelyOnDrop && line.SourceAction != null && payload?.SourceFace != null)
            combat.ConsumeImmediateActionAfterAttackAllAssign(payload.SourceFace, line.SourceAction);
    }

    private List<EnemyController> CollectAliveEnemies()
    {
        var list = new List<EnemyController>();
        if (combat?.ActiveEnemies == null)
            return list;

        for (var i = 0; i < combat.ActiveEnemies.Count; i++)
        {
            var enemy = combat.ActiveEnemies[i];
            if (enemy != null && enemy.IsAlive)
                list.Add(enemy);
        }

        return list;
    }

    /// <summary>
    /// When exactly one enemy is alive (single-enemy fight or the last survivor), returns it so enemy-targeted outcomes fly into
    /// that enemy's element container instead of the shared player pool.
    /// </summary>
    private bool TryResolveSoloFlyEnemy(out EnemyController soloEnemy)
    {
        soloEnemy = null;
        var alive = CollectAliveEnemies();
        if (alive.Count != 1)
            return false;

        soloEnemy = alive[0];
        return soloEnemy != null;
    }

    /// <summary>
    /// Fly target for one attack-all copy. Prefers the enemy's own element pool / drop target (multi-enemy), and falls back to the
    /// shared player Element Container when the enemy has no per-enemy targeting wired (single-enemy / Main Enemy).
    /// </summary>
    private RectTransform ResolveEnemyFlyTargetRect(EnemyController enemy, RollOutcomeVisualLine line, out bool isEnemyOwnPool)
    {
        isEnemyOwnPool = false;

        if (enemy?.AssignedElementPool != null)
        {
            var poolTarget = enemy.AssignedElementPool.GetFlyTargetRect(line.RowKey);
            if (poolTarget != null)
            {
                isEnemyOwnPool = true;
                return poolTarget;
            }
        }

        if (enemy?.DropTarget != null)
        {
            isEnemyOwnPool = true;
            return enemy.DropTarget.Rect;
        }

        return storedActionsPoolDisplay != null ? storedActionsPoolDisplay.GetFlyTargetRect(line.RowKey) : null;
    }

    private IEnumerator FlyLineToEnemyAssignRoutine(
        DiceRollVisualPayload payload,
        RectTransform rt,
        Vector2 start,
        Vector2 mid,
        Vector2 end,
        RollOutcomeVisualLine line,
        EnemyController enemy,
        bool applyToSharedPool)
    {
        if (rt == null)
            yield break;

        float dur = Mathf.Max(0.01f, flyDurationSeconds);
        float t = 0f;
        while (t < dur)
        {
            if (rt == null)
                yield break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float eased = flyEase != null ? flyEase.Evaluate(u) : u;
            SetLocalXY(rt, QuadraticBezier(start, mid, end, eased));
            yield return null;
        }

        if (rt == null)
            yield break;

        SetLocalXY(rt, end);

        if (payload?.SourceFace != null && combat != null && enemy != null)
        {
            combat.AssignRolledOutcomePieceToEnemy(
                payload.SourceFace,
                line.SourceAction,
                enemy,
                line,
                line.ResolvesImmediatelyOnDrop);
        }

        // Single-enemy / Main Enemy fallback: only increment the shared pool when this enemy has no per-enemy pool.
        if (applyToSharedPool && enemy?.AssignedElementPool == null && storedActionsPoolDisplay != null && storedActionsPoolDisplay.UsesFlyoutIncrementMode)
            storedActionsPoolDisplay.ApplyPoolDelta(line.RowKey, line.Amount, line.IconOverride, line.BackgroundOverride);

        Destroy(rt.gameObject);
    }

    private void TryNotifyFaceOutcomesSubmittedIfReady(DiceRollVisualPayload payload)
    {
        if (payload?.SourceFace == null || combat == null)
            return;
        if (HasPendingTokensForFace(payload.SourceFace))
            return;

        combat.NotifyFaceOutcomesSubmitted(payload.SourceFace);
    }

    private bool HasPendingTokensForFace(FaceResult face)
    {
        var assignment = ResolveTargetAssignment();
        return assignment != null && assignment.HasPendingTokensForFace(face);
    }

    private void TryBeginDieDissolveForPayload(DiceRollVisualPayload payload)
    {
        if (payload?.SourceFace != null && combat != null && combat.FaceBlocksDieDissolveForPendingReroll(payload.SourceFace))
            return;

        TryBeginDieDissolve(payload != null ? payload.DieTransform : null);
    }

    private void TryBeginDieDissolve(Transform dieTransform)
    {
        if (dieTransform == null)
            return;

        if (combat != null && combat.DieTransformBlocksDissolveForDieToDieDeferred(dieTransform))
            return;

        var spawner = combat != null ? combat.spawner : null;
        if (spawner == null)
            return;

        spawner.BeginDissolveAndDestroyDie(dieTransform.gameObject);
    }

    void ParkRerollFlyoutUntilLaunch(
        FaceResult sourceFace,
        RollOutcomeVisualLine line,
        Vector2 anchoredPosition,
        RectTransform parkedRect)
    {
        if (sourceFace == null || sourceFace.BatchGatherIndex < 0 || parkedRect == null)
            return;

        var launchIcon = new DieToDieLaunchIcon(
            line.IconOverride,
            line.BackgroundOverride != null
                ? line.BackgroundOverride
                : GameIconCatalog.TryGetPoolRowBackground(line.RowKey));

        if (!launchIcon.HasAny)
            return;

        _parkedRerollByBatchGatherIndex[sourceFace.BatchGatherIndex] = new ParkedRerollFlyoutEntry
        {
            ParkedRect = parkedRect,
            LaunchIcon = launchIcon,
            AnchoredPosition = anchoredPosition,
        };
    }

    /// <summary>
    /// Duplicates the single parked reroll row (if any) once per target, flies each to its die, then destroys.
    /// Falls back to spawning above <paramref name="sourceDie"/> when no parked row exists (pre-gather player-choice reroll).
    /// </summary>
    public IEnumerator CoLaunchParkedRerollFlyouts(
        int sourceBatchGatherIndex,
        Transform sourceDie,
        IReadOnlyList<Transform> targetDice,
        DieToDieLaunchIcon fallbackLaunchIcon,
        float? flightDurationOverride = null)
    {
        if (targetDice == null || targetDice.Count == 0)
            yield break;
        if (canvas == null || flyoutParent == null || flyoutPoolIconPrefab == null)
        {
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{name}': cannot launch parked reroll flyouts — assign canvas, flyoutParent, and flyoutPoolIconPrefab.");
            yield break;
        }

        var launchIcon = fallbackLaunchIcon;
        Vector2 stackOrigin;
        var hasParkedStart = false;

        if (sourceBatchGatherIndex >= 0
            && _parkedRerollByBatchGatherIndex.TryGetValue(sourceBatchGatherIndex, out var parked))
        {
            launchIcon = parked.LaunchIcon;
            stackOrigin = parked.AnchoredPosition;
            hasParkedStart = true;
            if (parked.ParkedRect != null)
                Destroy(parked.ParkedRect.gameObject);
            _parkedRerollByBatchGatherIndex.Remove(sourceBatchGatherIndex);
        }
        else if (sourceDie != null
                 && WorldPointToParentLocal(sourceDie.position + Vector3.up * worldOffsetAboveDie, flyoutParent, out var anchorFlyoutLocal))
        {
            stackOrigin = anchorFlyoutLocal + Vector2.right * layoutOffsetX;
        }
        else
        {
            yield break;
        }

        if (!launchIcon.HasAny)
            yield break;

        var rowKey = PoolRowKey.Custom("DieToDieReroll");
        var flyDuration = Mathf.Max(0.01f, flightDurationOverride ?? flyDurationSeconds);
        var lineRects = new List<RectTransform>();
        var targetTransforms = new List<Transform>();

        for (var i = 0; i < targetDice.Count; i++)
        {
            var target = targetDice[i];
            if (target == null)
                continue;

            var icon = Instantiate(flyoutPoolIconPrefab, flyoutParent);
            var rt = icon.transform as RectTransform;
            if (rt == null)
            {
                Debug.LogError("DiceRollOutcomeFlyoutController: flyoutPoolIconPrefab root must have a RectTransform.");
                Destroy(icon.gameObject);
                continue;
            }

            icon.SetupForDieToDieActionFlyout(rowKey, launchIcon.Icon, launchIcon.Background);
            SetLocalXY(rt, stackOrigin);
            rt.localScale = Vector3.one * EvaluateSpawnScaleMultiplier(1f);

            var targetBatchIndex = -1;
            if (combat != null && combat.spawner != null)
                targetBatchIndex = combat.spawner.GetIndexOfActiveDie(target.gameObject);
            if (targetBatchIndex >= 0)
                _inFlightDieToDieActionIconByTargetBatch[targetBatchIndex] = rt;

            lineRects.Add(rt);
            targetTransforms.Add(target);
        }

        if (lineRects.Count == 0)
            yield break;

        if (!hasParkedStart && lineRects.Count > 1)
        {
            for (var i = 1; i < lineRects.Count; i++)
                SetLocalXY(lineRects[i], stackOrigin);
        }

        var flyCoroutines = new List<Coroutine>();
        for (var i = 0; i < lineRects.Count; i++)
        {
            var targetBatchIndex = -1;
            if (combat != null && combat.spawner != null && targetTransforms[i] != null)
                targetBatchIndex = combat.spawner.GetIndexOfActiveDie(targetTransforms[i].gameObject);

            flyCoroutines.Add(StartCoroutine(CoFlyDieToDieActionLine(
                lineRects[i],
                stackOrigin,
                targetTransforms[i],
                flyDuration,
                targetBatchIndex)));
        }

        foreach (var c in flyCoroutines)
        {
            if (c != null)
                yield return c;
        }
    }

    /// <summary>
    /// Duplicates the parked Increase Other row once per target, flies each to its die, applies bonus on arrival.
    /// </summary>
    public IEnumerator CoLaunchParkedIncreaseOtherFlyouts(
        int sourceBatchGatherIndex,
        Transform sourceDie,
        IReadOnlyList<Transform> targetDice,
        DieToDieLaunchIcon fallbackLaunchIcon,
        int bonusAmount,
        System.Action<int> onTargetArrived,
        float? flightDurationOverride = null)
    {
        if (targetDice == null || targetDice.Count == 0)
            yield break;
        if (canvas == null || flyoutParent == null || flyoutPoolIconPrefab == null)
        {
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{name}': cannot launch Increase Other flyouts — assign canvas, flyoutParent, and flyoutPoolIconPrefab.");
            yield break;
        }

        var launchIcon = fallbackLaunchIcon;
        var displayAmount = bonusAmount;
        Vector2 stackOrigin;
        var hasParkedStart = false;

        if (sourceBatchGatherIndex >= 0
            && _parkedIncreaseOtherByBatchGatherIndex.TryGetValue(sourceBatchGatherIndex, out var parked))
        {
            launchIcon = parked.LaunchIcon;
            displayAmount = parked.BonusAmount > 0 ? parked.BonusAmount : bonusAmount;
            stackOrigin = parked.AnchoredPosition;
            hasParkedStart = true;
            RemoveParkedIncreaseOtherSourceVisual(sourceBatchGatherIndex);
        }
        else if (sourceDie != null
                 && WorldPointToParentLocal(sourceDie.position + Vector3.up * worldOffsetAboveDie, flyoutParent, out var anchorFlyoutLocal))
        {
            stackOrigin = anchorFlyoutLocal + Vector2.right * layoutOffsetX;
        }
        else
        {
            yield break;
        }

        if (!launchIcon.HasAny)
            yield break;

        var rowKey = PoolRowKey.Custom(ActionVisualId.IncreaseOtherElements.ToString());
        var flyDuration = Mathf.Max(0.01f, flightDurationOverride ?? flyDurationSeconds);
        var lineRects = new List<RectTransform>();
        var targetTransforms = new List<Transform>();

        for (var i = 0; i < targetDice.Count; i++)
        {
            var target = targetDice[i];
            if (target == null)
                continue;

            var icon = Instantiate(flyoutPoolIconPrefab, flyoutParent);
            var rt = icon.transform as RectTransform;
            if (rt == null)
            {
                Debug.LogError("DiceRollOutcomeFlyoutController: flyoutPoolIconPrefab root must have a RectTransform.");
                Destroy(icon.gameObject);
                continue;
            }

            if (displayAmount > 0)
                icon.SetupForDiceRollFlyout(rowKey, launchIcon.Icon, displayAmount, launchIcon.Background);
            else
                icon.SetupForDieToDieActionFlyout(rowKey, launchIcon.Icon, launchIcon.Background);

            SetLocalXY(rt, stackOrigin);
            rt.localScale = Vector3.one * EvaluateSpawnScaleMultiplier(1f);

            var targetBatchIndex = -1;
            if (combat != null && combat.spawner != null)
                targetBatchIndex = combat.spawner.GetIndexOfActiveDie(target.gameObject);
            if (targetBatchIndex >= 0)
                _inFlightDieToDieActionIconByTargetBatch[targetBatchIndex] = rt;

            lineRects.Add(rt);
            targetTransforms.Add(target);
        }

        if (lineRects.Count == 0)
            yield break;

        if (!hasParkedStart && lineRects.Count > 1)
        {
            for (var i = 1; i < lineRects.Count; i++)
                SetLocalXY(lineRects[i], stackOrigin);
        }

        var flyCoroutines = new List<Coroutine>();
        for (var i = 0; i < lineRects.Count; i++)
        {
            var targetIndex = i;
            var targetBatchIndex = -1;
            if (combat != null && combat.spawner != null && targetTransforms[i] != null)
                targetBatchIndex = combat.spawner.GetIndexOfActiveDie(targetTransforms[i].gameObject);

            flyCoroutines.Add(StartCoroutine(CoFlyDieToDieActionLine(
                lineRects[i],
                stackOrigin,
                targetTransforms[i],
                flyDuration,
                targetBatchIndex,
                () => onTargetArrived?.Invoke(targetIndex))));
        }

        foreach (var c in flyCoroutines)
        {
            if (c != null)
                yield return c;
        }
    }

    IEnumerator CoFlyDieToDieActionLine(
        RectTransform rt,
        Vector2 startAnchored,
        Transform targetDie,
        float durationSeconds,
        int targetBatchIndex,
        System.Action onArrived = null)
    {
        if (rt == null || targetDie == null)
            yield break;

        try
        {
            var t = 0f;
            while (t < durationSeconds)
            {
                if (rt == null || targetDie == null)
                    yield break;

                t += Time.deltaTime;
                var u = Mathf.Clamp01(t / durationSeconds);
                var eased = flyEase != null ? flyEase.Evaluate(u) : u;

                if (!WorldPointToParentLocal(targetDie.position + Vector3.up * worldOffsetAboveDie, flyoutParent, out var endAnchored))
                    yield break;

                endAnchored += Vector2.right * layoutOffsetX;
                var mid = (startAnchored + endAnchored) * 0.5f + Vector2.up * arcHeightPixels;
                SetLocalXY(rt, QuadraticBezier(startAnchored, mid, endAnchored, eased));
                yield return null;
            }

            onArrived?.Invoke();
        }
        finally
        {
            if (targetBatchIndex >= 0)
                _inFlightDieToDieActionIconByTargetBatch.Remove(targetBatchIndex);

            if (rt != null)
                Destroy(rt.gameObject);
        }
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static void DrainQueueAndReportFinished(Queue<DiceRollVisualPayload> queue)
    {
        while (queue.Count > 0)
        {
            var payload = queue.Dequeue();
            payload?.ReportVisualFinished();
        }
    }

    private IEnumerator PlayDieActivationFeedback(Transform dieTransform)
    {
        if (dieTransform == null)
            yield break;

        var renderers = dieTransform.GetComponentsInChildren<Renderer>(true);
        var blocks = new MaterialPropertyBlock[renderers.Length];
        var originalSelfLitByRenderer = new float[renderers.Length];
        var hasSelfLitByRenderer = new bool[renderers.Length];

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var shared = GetFirstSelfLitMaterial(renderer);
            if (shared == null)
                continue;

            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            blocks[i] = mpb;
            hasSelfLitByRenderer[i] = true;
            originalSelfLitByRenderer[i] = shared.GetFloat("_SelfLitIntensity");
            mpb.SetFloat("_SelfLitIntensity", originalSelfLitByRenderer[i]);
            renderer.SetPropertyBlock(mpb);
        }

        var localOrigin = dieTransform.localPosition;
        var duration = Mathf.Max(0.01f, Mathf.Max(shakeDurationSeconds, selfLitPulseDurationSeconds));
        var elapsed = 0f;
        var pulseDuration = Mathf.Max(0.01f, selfLitPulseDurationSeconds);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            var pulseT = Mathf.Clamp01(elapsed / pulseDuration);
            var pulse = 1f - Mathf.Abs(2f * pulseT - 1f);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!hasSelfLitByRenderer[i]) continue;
                var renderer = renderers[i];
                if (renderer == null) continue;
                var mpb = blocks[i] ?? new MaterialPropertyBlock();
                var value = Mathf.Lerp(originalSelfLitByRenderer[i], 1f, pulse);
                mpb.SetFloat("_SelfLitIntensity", value);
                renderer.SetPropertyBlock(mpb);
            }

            var offset = Random.insideUnitSphere * shakeAmplitude;
            dieTransform.localPosition = localOrigin + offset;
            yield return null;
        }

        if (dieTransform == null)
            yield break;

        dieTransform.localPosition = localOrigin;

        for (var i = 0; i < renderers.Length; i++)
        {
            if (!hasSelfLitByRenderer[i]) continue;
            var renderer = renderers[i];
            if (renderer == null) continue;
            var mpb = blocks[i] ?? new MaterialPropertyBlock();
            mpb.SetFloat("_SelfLitIntensity", originalSelfLitByRenderer[i]);
            renderer.SetPropertyBlock(mpb);
        }
    }

    private static Material GetFirstSelfLitMaterial(Renderer renderer)
    {
        if (renderer == null) return null;
        var mats = renderer.sharedMaterials;
        if (mats == null || mats.Length == 0) return null;
        for (var i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (mat != null && mat.HasProperty("_SelfLitIntensity"))
                return mat;
        }

        return null;
    }

    /// <summary>Self-lit pulse + shake used after gather and when Increase Other projectiles hit a target die.</summary>
    public void PlayDieActivationFeedbackOnDie(Transform dieTransform)
    {
        if (dieTransform == null)
            return;

        StartCoroutine(PlayDieActivationFeedback(dieTransform));
    }

    /// <summary>Projects a world point into a UI parent's local space (pivot-relative), suitable for <see cref="Transform.localPosition"/>.</summary>
    private bool WorldPointToParentLocal(Vector3 world, RectTransform parent, out Vector2 localPoint)
    {
        localPoint = default;
        if (parent == null)
            return false;

        Camera worldCam = worldCamera != null ? worldCamera : Camera.main;
        if (worldCam == null)
        {
            Debug.LogError("DiceRollOutcomeFlyoutController: Assign worldCamera (or tag MainCamera) for 3D → UI projection.");
            return false;
        }

        Vector3 screen3 = worldCam.WorldToScreenPoint(world);
        if (screen3.z <= 0f)
            return false;
        var screen = new Vector2(screen3.x, screen3.y);

        // Resolve the event camera from the parent's own canvas so projection is correct even when this controller
        // lives on its own separate canvas (Screen Space - Camera / Overlay / World Space).
        var parentCanvas = parent.GetComponentInParent<Canvas>();
        Camera uiCam = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (parentCanvas.worldCamera != null ? parentCanvas.worldCamera : worldCam)
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out localPoint);
    }

    /// <summary>
    /// Projects the center of a UI target rect into <paramref name="parent"/>'s local space using each rect's own canvas camera.
    /// Use this for UI→UI fly targets (pool rows, status bars). <see cref="WorldPointToParentLocal"/> is only valid for the 3D die's
    /// world position — feeding a Screen-Space-Overlay rect's world corners (screen pixels) through the 3D camera fails projection.
    /// </summary>
    private bool UiRectCenterToParentLocal(RectTransform target, RectTransform parent, out Vector2 localPoint)
    {
        localPoint = default;
        if (target == null || parent == null)
            return false;

        Vector3 worldCenter = GetWorldCornersCenter(target);

        var targetCanvas = target.GetComponentInParent<Canvas>();
        Camera targetCam = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCam, worldCenter);

        var parentCanvas = parent.GetComponentInParent<Canvas>();
        Camera parentCam = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (parentCanvas.worldCamera != null ? parentCanvas.worldCamera : targetCam)
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, parentCam, out localPoint);
    }

    private static Vector3 GetWorldCornersCenter(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        return (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
    }

    private RectTransform ResolveFlyTargetRect(RollOutcomeVisualLine line)
    {
        if (line.FlyToPlayerStatusBar && playerStatusBarFlyTarget != null)
            return playerStatusBarFlyTarget;

        var statusTarget = ResolveStatusTarget(line);
        if (ShouldRouteStatusToStatusBar(line, statusTarget))
        {
            if (statusTarget.Value == StatusEffectTarget.Player && playerStatusBarFlyTarget != null)
                return playerStatusBarFlyTarget;
            if (statusTarget.Value == StatusEffectTarget.Enemy && enemyStatusBarFlyTarget != null)
                return enemyStatusBarFlyTarget;
        }

        return storedActionsPoolDisplay != null ? storedActionsPoolDisplay.GetFlyTargetRect(line.RowKey) : null;
    }

    private StatusEffectTarget? ResolveStatusTarget(RollOutcomeVisualLine line)
    {
        if (GameIconCatalog.TryGetStatusTargetForPoolRow(line.RowKey, out var statusTarget))
            return statusTarget;
        return null;
    }

    private bool ShouldApplyPoolDelta(RollOutcomeVisualLine line, StatusEffectTarget? statusTarget)
    {
        // Status-targeted flyouts that route to status bars are visual-only and must not mutate pool display rows.
        if (!ShouldRouteStatusToStatusBar(line, statusTarget))
            return true;
        return false;
    }

    private bool ShouldRouteStatusToStatusBar(RollOutcomeVisualLine line, StatusEffectTarget? statusTarget)
    {
        if (line.FlyToPlayerStatusBar && playerStatusBarFlyTarget != null)
            return true;

        // Deferred rows (non-immediate actions) must always fly to the element container.
        // Only immediate visual-only status rows route to status bars.
        if (!line.IsVisualFlyoutOnly || !statusTarget.HasValue)
            return false;
        if (statusTarget.Value == StatusEffectTarget.Player)
            return playerStatusBarFlyTarget != null;
        if (statusTarget.Value == StatusEffectTarget.Enemy)
            return enemyStatusBarFlyTarget != null;
        return false;
    }

    private bool BeginFreezeStatusBar(StatusEffectTarget target)
    {
        if (target == StatusEffectTarget.Player)
        {
            _playerStatusFreezeCount++;
            if (_playerStatusFreezeCount == 1 && playerStatusBarUI != null)
                playerStatusBarUI.SetVisualRefreshFrozen(true);
            return true;
        }

        if (target == StatusEffectTarget.Enemy)
        {
            _enemyStatusFreezeCount++;
            if (_enemyStatusFreezeCount == 1 && enemyStatusBarUI != null)
                enemyStatusBarUI.SetVisualRefreshFrozen(true);
            return true;
        }

        return false;
    }

    private void EndFreezeStatusBar(StatusEffectTarget target)
    {
        if (target == StatusEffectTarget.Player)
        {
            if (_playerStatusFreezeCount > 0)
                _playerStatusFreezeCount--;
            if (_playerStatusFreezeCount == 0 && playerStatusBarUI != null)
                playerStatusBarUI.SetVisualRefreshFrozen(false);
            return;
        }

        if (target == StatusEffectTarget.Enemy)
        {
            if (_enemyStatusFreezeCount > 0)
                _enemyStatusFreezeCount--;
            if (_enemyStatusFreezeCount == 0 && enemyStatusBarUI != null)
                enemyStatusBarUI.SetVisualRefreshFrozen(false);
        }
    }

    private void ForceUnfreezeStatusBars()
    {
        _playerStatusFreezeCount = 0;
        _enemyStatusFreezeCount = 0;
        if (playerStatusBarUI != null)
            playerStatusBarUI.SetVisualRefreshFrozen(false);
        if (enemyStatusBarUI != null)
            enemyStatusBarUI.SetVisualRefreshFrozen(false);
    }
}
