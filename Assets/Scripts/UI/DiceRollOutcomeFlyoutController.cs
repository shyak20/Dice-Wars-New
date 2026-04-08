using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns stacked outcome rows above a 3D die, waits, then flies each row along a curved path (bezier + ease curve) into <see cref="ElementPoolDisplay"/>.
/// </summary>
public class DiceRollOutcomeFlyoutController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform flyoutParent;
    [SerializeField] private ElementPoolDisplay elementPoolDisplay;
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

    [Header("Motion (A → B along quadratic bezier)")]
    [SerializeField] private AnimationCurve flyEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float arcHeightPixels = 96f;

    private void Awake()
    {
        if (canvas == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': canvas is not assigned!");
        if (flyoutParent == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': flyoutParent is not assigned!");
        if (elementPoolDisplay == null)
            Debug.LogError($"DiceRollOutcomeFlyoutController on '{gameObject.name}': elementPoolDisplay is not assigned!");
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

        if (canvas == null || flyoutParent == null || elementPoolDisplay == null || linePrefab == null)
        {
            payload.ReportVisualFinished();
            return;
        }

        if (!elementPoolDisplay.UsesFlyoutIncrementMode)
        {
            payload.ReportVisualFinished();
            return;
        }

        StartCoroutine(PlayFlyoutRoutine(payload));
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
                    var sprite = line.IconOverride != null ? line.IconOverride : elementPoolDisplay.GetPoolTypeSprite(line.Type);
                    view.Setup(sprite, line.Amount);
                }
                else
                    Debug.LogError("DiceRollOutcomeFlyoutController: linePrefab should include RollOutcomeFlyoutLineView.");

                // First line at anchor (die + worldOffsetAboveDie → canvas). Each next line stacks above the previous by lineSpacing.
                rt.anchoredPosition = anchorLocal + Vector2.up * (i * lineSpacing);
                lineRects.Add(rt);
            }

            if (lineRects.Count == 0) yield break;

            if (waitBeforeFlySeconds > 0f)
                yield return new WaitForSeconds(waitBeforeFlySeconds);

            var flyCoroutines = new List<Coroutine>();
            for (int i = 0; i < payload.Lines.Count && i < lineRects.Count; i++)
            {
                var line = payload.Lines[i];
                RectTransform target = elementPoolDisplay.GetFlyTargetRect(line.Type);
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
        elementPoolDisplay.ApplyPoolDelta(line.Type, line.Amount, line.IconOverride);
        Destroy(rt.gameObject);
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
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
