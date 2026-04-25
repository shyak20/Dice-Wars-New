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

    [Header("Prefab")]
    [SerializeField] private GameObject linePrefab;

    [Header("Layout")]
    [Tooltip("World-units: lift the stack anchor above the die. First line starts here.")]
    [SerializeField] private float worldOffsetAboveDie = 0.75f;
    [Tooltip("Canvas pixels: each additional line is placed this far above the previous line (toward top of screen).")]
    [SerializeField] private float lineSpacing = 36f;

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

    private void Awake()
    {
        if (canvas == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': canvas is not assigned!");
        if (flyoutParent == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': flyoutParent is not assigned!");
        if (storedActionsPoolDisplay == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': storedActionsPoolDisplay is not assigned!");
        if (linePrefab == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': linePrefab is not assigned!");
        if (worldCamera == null)
            worldCamera = Camera.main;
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

        if (canvas == null || flyoutParent == null || storedActionsPoolDisplay == null || linePrefab == null)
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
        try
        {
            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) yield break;

            Camera eventCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : worldCamera;

            Vector3 anchorWorld = payload.WorldAnchor + Vector3.up * worldOffsetAboveDie;
            if (!WorldToCanvasLocal(canvasRect, eventCam, anchorWorld, out Vector2 anchorLocal))
                yield break;

            var lineRects = new List<RectTransform>();
            for (int i = 0; i < payload.Lines.Count; i++)
            {
                var line = payload.Lines[i];
                GameObject row = Instantiate(linePrefab, flyoutParent);
                var rt = row.transform as RectTransform;
                if (rt == null)
                {
                    Debug.LogError("DiceRollOutcomeFlyoutController: linePrefab root must have a RectTransform.");
                    Destroy(row);
                    continue;
                }

                var view = row.GetComponent<RollOutcomeFlyoutLineView>();
                if (view != null)
                {
                    var sprite = line.IconOverride != null ? line.IconOverride : storedActionsPoolDisplay.GetPoolRowSprite(line.RowKey);
                    view.Setup(sprite, line.Amount);
                }
                else
                    Debug.LogError("DiceRollOutcomeFlyoutController: linePrefab should include RollOutcomeFlyoutLineView.");

                // First line at anchor (die + worldOffsetAboveDie → canvas). Each next line stacks above the previous by lineSpacing.
                rt.anchoredPosition = anchorLocal + Vector2.up * (i * lineSpacing);
                lineRects.Add(rt);
            }

            if (lineRects.Count == 0) yield break;

            if (payload.DieTransform != null)
                yield return PlayDieActivationFeedback(payload.DieTransform);

            if (waitBeforeFlySeconds > 0f)
                yield return new WaitForSeconds(waitBeforeFlySeconds);

            var flyCoroutines = new List<Coroutine>();
            for (int i = 0; i < payload.Lines.Count && i < lineRects.Count; i++)
            {
                var line = payload.Lines[i];
                RectTransform target = storedActionsPoolDisplay.GetFlyTargetRect(line.RowKey);
                if (target == null)
                {
                    Destroy(lineRects[i].gameObject);
                    continue;
                }

                if (!WorldToCanvasLocal(canvasRect, eventCam, GetWorldCornersCenter(target), out Vector2 endLocal))
                {
                    Destroy(lineRects[i].gameObject);
                    continue;
                }

                Vector2 startLocal = lineRects[i].anchoredPosition;
                Vector2 mid = (startLocal + endLocal) * 0.5f + Vector2.up * arcHeightPixels;
                flyCoroutines.Add(StartCoroutine(FlyLineRoutine(lineRects[i], startLocal, mid, endLocal, line)));
            }

            foreach (var c in flyCoroutines)
            {
                if (c != null)
                    yield return c;
            }
        }
        finally
        {
            payload.ReportVisualFinished();
        }
    }

    private IEnumerator FlyLineRoutine(RectTransform rt, Vector2 start, Vector2 mid, Vector2 end, RollOutcomeVisualLine line)
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
        if (storedActionsPoolDisplay != null && storedActionsPoolDisplay.UsesFlyoutIncrementMode)
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

    private bool WorldToCanvasLocal(RectTransform canvasRect, Camera eventCamera, Vector3 world, out Vector2 localPoint)
    {
        localPoint = default;
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogError("DiceRollOutcomeFlyoutController: Assign worldCamera (or tag MainCamera) for 3D → UI projection.");
            return false;
        }

        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f)
            return false;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCamera, out localPoint);
    }

    private static Vector3 GetWorldCornersCenter(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        return (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
    }
}
