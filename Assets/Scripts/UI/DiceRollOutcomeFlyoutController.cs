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

    private readonly Queue<DiceRollVisualPayload> regularPayloadQueue = new Queue<DiceRollVisualPayload>();
    private readonly Queue<DiceRollVisualPayload> deferredPayloadQueue = new Queue<DiceRollVisualPayload>();
    private Coroutine queueRoutine;
    private int _playerStatusFreezeCount;
    private int _enemyStatusFreezeCount;

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
    }

    private void OnDisable()
    {
        CombatEvents.OnDiceRollVisualFeedback -= HandleRollVisual;
        if (queueRoutine != null)
        {
            StopCoroutine(queueRoutine);
            queueRoutine = null;
        }
        DrainQueueAndReportFinished(regularPayloadQueue);
        DrainQueueAndReportFinished(deferredPayloadQueue);
        ForceUnfreezeStatusBars();
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
            payload.ReportVisualFinished();
            return;
        }

        if (canvas == null || flyoutParent == null || storedActionsPoolDisplay == null || flyoutPoolIconPrefab == null)
        {
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

    private IEnumerator PlayFlyoutRoutine(DiceRollVisualPayload payload)
    {
        var frozePlayerForFlyout = false;
        var frozeEnemyForFlyout = false;
        try
        {
            Vector3 dieWorld = payload.DieTransform != null ? payload.DieTransform.position : payload.WorldAnchor;
            Vector3 stackAnchorWorld = dieWorld + Vector3.up * worldOffsetAboveDie;
            if (!WorldPointToFlyoutParentLocal(stackAnchorWorld, out Vector2 anchorLocal))
                yield break;

            // Status is applied (and OnEffectsChanged queued a refresh) before this event fires.
            // Freeze here synchronously — before any yield — so LateUpdate cannot refresh stacks until the fly lands.
            TryBeginStatusBarFlyoutFreeze(payload, ref frozePlayerForFlyout, ref frozeEnemyForFlyout);

            Vector2 stackOriginLocal = anchorLocal + Vector2.right * layoutOffsetX;

            var lineRects = new List<RectTransform>();
            var stackRestAnchored = new List<Vector2>();
            var spawnRootBaseScales = new List<Vector3>();
            var spawnRoutines = new List<Coroutine>();

            for (int i = 0; i < payload.Lines.Count; i++)
            {
                var line = payload.Lines[i];
                var icon = Instantiate(flyoutPoolIconPrefab, flyoutParent);
                var rt = icon.transform as RectTransform;
                if (rt == null)
                {
                    Debug.LogError("DiceRollOutcomeFlyoutController: flyoutPoolIconPrefab root must have a RectTransform.");
                    Destroy(icon.gameObject);
                    continue;
                }

                var sprite = line.IconOverride != null ? line.IconOverride : storedActionsPoolDisplay.GetPoolRowSprite(line.RowKey);
                icon.SetupForDiceRollFlyout(line.RowKey, sprite, line.Amount);

                Vector3 spawnBaseLocalScale = rt.localScale;
                Vector2 basePos = stackOriginLocal + Vector2.up * (i * lineSpacing);
                Vector2 restAnchored = basePos + Vector2.up * spawnYOffsetOverTime.Evaluate(1f);
                bool animateSpawn = spawnAlongYDurationSeconds > 1e-4f;
                if (!animateSpawn)
                {
                    rt.anchoredPosition = restAnchored;
                    rt.localScale = spawnBaseLocalScale * EvaluateSpawnScaleMultiplier(1f);
                }
                else
                {
                    rt.anchoredPosition = basePos + Vector2.up * spawnYOffsetOverTime.Evaluate(0f);
                    rt.localScale = spawnBaseLocalScale * EvaluateSpawnScaleMultiplier(0f);
                    spawnRoutines.Add(StartCoroutine(CoSpawnPresentationMotion(rt, basePos, spawnBaseLocalScale)));
                }

                lineRects.Add(rt);
                stackRestAnchored.Add(restAnchored);
                spawnRootBaseScales.Add(spawnBaseLocalScale);
            }

            foreach (var c in spawnRoutines)
            {
                if (c != null)
                    yield return c;
            }

            if (lineRects.Count == 0) yield break;

            if (payload.DieTransform != null)
                yield return PlayDieActivationFeedback(payload.DieTransform);

            if (waitBeforeFlySeconds > 0f)
                yield return new WaitForSeconds(waitBeforeFlySeconds);

            float spawnScaleEnd = EvaluateSpawnScaleMultiplier(1f);
            for (int s = 0; s < lineRects.Count; s++)
            {
                var rt = lineRects[s];
                rt.anchoredPosition = stackRestAnchored[s];
                rt.localScale = spawnRootBaseScales[s] * spawnScaleEnd;
            }

            var flyCoroutines = new List<Coroutine>();
            for (int i = 0; i < payload.Lines.Count && i < lineRects.Count; i++)
            {
                var line = payload.Lines[i];
                RectTransform target = ResolveFlyTargetRect(line);
                if (target == null)
                {
                    Destroy(lineRects[i].gameObject);
                    continue;
                }

                if (!WorldPointToFlyoutParentLocal(GetWorldCornersCenter(target), out Vector2 endLocal))
                {
                    Destroy(lineRects[i].gameObject);
                    continue;
                }

                Vector2 startLocal = stackRestAnchored[i];
                Vector2 mid = (startLocal + endLocal) * 0.5f + Vector2.up * arcHeightPixels;
                var statusTarget = ResolveStatusTarget(line);
                var applyPoolDelta = ShouldApplyPoolDelta(line, statusTarget);
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
            if (st == StatusEffectTarget.Player && playerStatusBarFlyTarget != null)
                needPlayer = true;
            else if (st == StatusEffectTarget.Enemy && enemyStatusBarFlyTarget != null)
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
            rt.anchoredPosition = baseAnchored + Vector2.up * y;
            rt.localScale = prefabRootBaseScale * EvaluateSpawnScaleMultiplier(u);
            yield return null;
        }

        rt.anchoredPosition = baseAnchored + Vector2.up * spawnYOffsetOverTime.Evaluate(1f);
        rt.localScale = prefabRootBaseScale * EvaluateSpawnScaleMultiplier(1f);
    }

    private IEnumerator FlyLineRoutine(RectTransform rt, Vector2 start, Vector2 mid, Vector2 end, RollOutcomeVisualLine line, bool applyPoolDeltaOnLanding)
    {
        float dur = Mathf.Max(0.01f, flyDurationSeconds);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = flyEase != null ? flyEase.Evaluate(u) : u;
            rt.anchoredPosition = QuadraticBezier(start, mid, end, e);
            yield return null;
        }

        rt.anchoredPosition = end;
        if (applyPoolDeltaOnLanding && storedActionsPoolDisplay != null && storedActionsPoolDisplay.UsesFlyoutIncrementMode)
            storedActionsPoolDisplay.ApplyPoolDelta(line.RowKey, line.Amount, line.IconOverride);
        Destroy(rt.gameObject);
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
            var pulse = 1f - Mathf.Abs(2f * pulseT - 1f); // 0->1->0
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

        if (dieTransform != null)
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

    /// <summary>Projects a world point (e.g. die or UI widget in world) into <see cref="flyoutParent"/> local space so <see cref="RectTransform.anchoredPosition"/> matches the visual.</summary>
    private bool WorldPointToFlyoutParentLocal(Vector3 world, out Vector2 localPoint)
    {
        localPoint = default;
        if (flyoutParent == null)
            return false;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogError("DiceRollOutcomeFlyoutController: Assign worldCamera (or tag MainCamera) for 3D → UI projection.");
            return false;
        }

        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f)
            return false;

        Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (canvas.worldCamera != null ? canvas.worldCamera : worldCamera)
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            flyoutParent, new Vector2(screen.x, screen.y), eventCam, out localPoint);
    }

    private static Vector3 GetWorldCornersCenter(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        return (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
    }

    private RectTransform ResolveFlyTargetRect(RollOutcomeVisualLine line)
    {
        var statusTarget = ResolveStatusTarget(line);
        if (statusTarget.HasValue)
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
        if (!statusTarget.HasValue)
            return true;
        if (statusTarget.Value == StatusEffectTarget.Player && playerStatusBarFlyTarget != null)
            return false;
        if (statusTarget.Value == StatusEffectTarget.Enemy && enemyStatusBarFlyTarget != null)
            return false;
        return true;
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
