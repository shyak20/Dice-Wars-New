using UnityEngine;

/// <summary>
/// Pins a world-space <see cref="Transform"/> to a screen position that tracks UI layout.
/// When <see cref="followUITarget"/> is assigned, the object follows that RectTransform exactly
/// (best for sitting a 3D object on a UI hand/sprite).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class ScreenAnchorTransform : MonoBehaviour
{
    public enum ScreenAnchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private ScreenAnchor anchor = ScreenAnchor.BottomCenter;
    [Tooltip("Distance from the camera along its forward axis (world units). Sets the depth plane the object is placed on.")]
    [SerializeField, Min(0.01f)] private float distanceFromCamera = 10f;

    [Header("Follow a UI element (recommended)")]
    [Tooltip("UI element to track (e.g. the hand Image). When set, the object follows this rect and Reference Pixel Offset is ignored.")]
    [SerializeField] private RectTransform followUITarget;
    [Tooltip("Point on the followed rect in normalized rect space (0,0 = bottom-left, 0.5,0.5 = center).")]
    [SerializeField] private Vector2 followTargetNormalizedPoint = new Vector2(0.5f, 0.5f);
    [Tooltip("Extra offset in the followed rect's local pixels (+X right, +Y up). Use this to nudge onto the palm center.")]
    [SerializeField] private Vector2 followLocalOffset;

    [Header("Canvas anchor fallback (when no follow target)")]
    [Tooltip("Canvas used for anchor math when Follow UI Target is empty.")]
    [SerializeField] private Canvas referenceCanvas;
    [Tooltip("Offset from the anchor in reference-resolution pixels (+X right, +Y up). Ignored when Follow UI Target is assigned.")]
    [SerializeField] private Vector2 referencePixelOffset;
    [Tooltip("Match the same reference resolution as your CanvasScaler (e.g. 1920x1080).")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("0 = match Width, 1 = match Height. Mirror your CanvasScaler's 'Match Width Or Height'.")]
    [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0f;

    [Header("Behaviour")]
    [Tooltip("When enabled, keeps the anchor inside Screen.safeArea so it respects mobile notches.")]
    [SerializeField] private bool respectSafeArea = true;
    [Tooltip("Also re-apply every frame so the anchor tracks a moving/rotating camera. Resolution & aspect changes are always tracked regardless of this flag.")]
    [SerializeField] private bool followMovingCamera = true;

    Camera _resolvedCamera;

    int _lastScreenWidth;
    int _lastScreenHeight;
    Rect _lastSafeArea;
    float _lastCameraAspect;
    bool _hasAppliedOnce;

    void OnEnable()
    {
        _hasAppliedOnce = false;
        if (followUITarget != null)
            Canvas.willRenderCanvases += HandleWillRenderCanvases;

        ApplyAnchor();
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
    }

    void LateUpdate()
    {
        if (followUITarget != null)
            return;

        if (followMovingCamera || ScreenOrCameraChanged())
            ApplyAnchor();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        _hasAppliedOnce = false;
        ApplyAnchor();
    }

    void HandleWillRenderCanvases()
    {
        if (followUITarget == null || !isActiveAndEnabled)
            return;

        ApplyAnchor();
    }

    bool ScreenOrCameraChanged()
    {
        if (!_hasAppliedOnce)
            return true;

        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            return true;

        if (Screen.safeArea != _lastSafeArea)
            return true;

        var cam = _resolvedCamera;
        return cam != null && !Mathf.Approximately(cam.aspect, _lastCameraAspect);
    }

    /// <summary>Recomputes world position from the current anchor settings.</summary>
    public void ApplyAnchor()
    {
        var cam = ResolveCamera();
        if (cam == null)
            return;

        var world = ScreenAnchorUtility.AnchorToWorld(
            cam,
            referenceCanvas,
            followUITarget,
            followTargetNormalizedPoint,
            followLocalOffset,
            anchor,
            referencePixelOffset,
            referenceResolution,
            matchWidthOrHeight,
            distanceFromCamera,
            respectSafeArea);

        transform.position = world;

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastSafeArea = Screen.safeArea;
        _lastCameraAspect = cam.aspect;
        _hasAppliedOnce = true;
    }

    public void SetAnchor(ScreenAnchor newAnchor)
    {
        anchor = newAnchor;
        ApplyAnchor();
    }

    Camera ResolveCamera()
    {
        if (targetCamera != null)
        {
            _resolvedCamera = targetCamera;
            return targetCamera;
        }

        if (_resolvedCamera != null && _resolvedCamera.isActiveAndEnabled)
            return _resolvedCamera;

        _resolvedCamera = Camera.main;
        if (_resolvedCamera == null && Application.isPlaying)
        {
            Debug.LogError(
                $"ScreenAnchorTransform on '{name}': assign targetCamera or tag a camera as MainCamera.",
                this);
        }

        return _resolvedCamera;
    }
}

/// <summary>Shared math for <see cref="ScreenAnchorTransform"/>.</summary>
public static class ScreenAnchorUtility
{
    public static Vector2 AnchorToViewport(ScreenAnchorTransform.ScreenAnchor anchor)
    {
        return anchor switch
        {
            ScreenAnchorTransform.ScreenAnchor.TopLeft => new Vector2(0f, 1f),
            ScreenAnchorTransform.ScreenAnchor.TopCenter => new Vector2(0.5f, 1f),
            ScreenAnchorTransform.ScreenAnchor.TopRight => new Vector2(1f, 1f),
            ScreenAnchorTransform.ScreenAnchor.MiddleLeft => new Vector2(0f, 0.5f),
            ScreenAnchorTransform.ScreenAnchor.MiddleCenter => new Vector2(0.5f, 0.5f),
            ScreenAnchorTransform.ScreenAnchor.MiddleRight => new Vector2(1f, 0.5f),
            ScreenAnchorTransform.ScreenAnchor.BottomLeft => new Vector2(0f, 0f),
            ScreenAnchorTransform.ScreenAnchor.BottomCenter => new Vector2(0.5f, 0f),
            ScreenAnchorTransform.ScreenAnchor.BottomRight => new Vector2(1f, 0f),
            _ => new Vector2(0.5f, 0.5f),
        };
    }

    public static float CanvasScaleFactor(Vector2 referenceResolution, float matchWidthOrHeight)
    {
        var refW = Mathf.Max(1f, referenceResolution.x);
        var refH = Mathf.Max(1f, referenceResolution.y);
        var logWidth = Mathf.Log(Mathf.Max(1, Screen.width) / refW, 2f);
        var logHeight = Mathf.Log(Mathf.Max(1, Screen.height) / refH, 2f);
        var logWeighted = Mathf.Lerp(logWidth, logHeight, Mathf.Clamp01(matchWidthOrHeight));
        return Mathf.Pow(2f, logWeighted);
    }

    public static Vector2 ApplySafeAreaToViewport(Vector2 viewport01)
    {
        var safe = Screen.safeArea;
        if (safe.width <= 0f || safe.height <= 0f)
            return viewport01;

        var fullW = Screen.width;
        var fullH = Screen.height;
        if (fullW <= 0f || fullH <= 0f)
            return viewport01;

        var safeMinX = safe.xMin / fullW;
        var safeMaxX = safe.xMax / fullW;
        var safeMinY = safe.yMin / fullH;
        var safeMaxY = safe.yMax / fullH;

        return new Vector2(
            Mathf.Lerp(safeMinX, safeMaxX, viewport01.x),
            Mathf.Lerp(safeMinY, safeMaxY, viewport01.y));
    }

    public static Vector3 AnchorToWorld(
        Camera placementCamera,
        Canvas referenceCanvas,
        RectTransform followUITarget,
        Vector2 followTargetNormalizedPoint,
        Vector2 followLocalOffset,
        ScreenAnchorTransform.ScreenAnchor anchor,
        Vector2 referencePixelOffset,
        Vector2 referenceResolution,
        float matchWidthOrHeight,
        float distanceFromCamera,
        bool respectSafeArea)
    {
        if (placementCamera == null)
            throw new System.ArgumentNullException(nameof(placementCamera));

        var depth = Mathf.Max(0.01f, distanceFromCamera);

        if (followUITarget != null && TryFollowRectTransformToWorld(
                placementCamera,
                followUITarget,
                followTargetNormalizedPoint,
                followLocalOffset,
                depth,
                out var followWorld))
        {
            return followWorld;
        }

        if (referenceCanvas != null && TryCanvasAnchorToScreenPoint(
                referenceCanvas,
                anchor,
                referencePixelOffset,
                respectSafeArea,
                out var canvasScreenPoint))
        {
            return placementCamera.ScreenToWorldPoint(new Vector3(canvasScreenPoint.x, canvasScreenPoint.y, depth));
        }

        var viewport = AnchorToViewport(anchor);
        if (respectSafeArea)
            viewport = ApplySafeAreaToViewport(viewport);

        var scale = CanvasScaleFactor(referenceResolution, matchWidthOrHeight);
        var pixelOffset = referencePixelOffset * scale;

        var screenX = viewport.x * Screen.width + pixelOffset.x;
        var screenY = viewport.y * Screen.height + pixelOffset.y;

        return placementCamera.ScreenToWorldPoint(new Vector3(screenX, screenY, depth));
    }

    /// <summary>
    /// Places the 3D object on the same screen ray as a point on a UI rect, at the requested depth.
    /// Uses one camera for both conversions so aspect changes stay locked to the UI element.
    /// </summary>
    public static bool TryFollowRectTransformToWorld(
        Camera placementCamera,
        RectTransform followUITarget,
        Vector2 normalizedPoint,
        Vector2 localOffset,
        float distanceFromCamera,
        out Vector3 worldPosition)
    {
        worldPosition = default;

        if (placementCamera == null || followUITarget == null)
            return false;

        var rect = followUITarget.rect;
        var localPoint = new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedPoint.x),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedPoint.y)) + localOffset;

        var uiWorld = followUITarget.TransformPoint(localPoint);
        var screen = placementCamera.WorldToScreenPoint(uiWorld);
        worldPosition = placementCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, distanceFromCamera));
        return true;
    }

    public static bool TryCanvasAnchorToScreenPoint(
        Canvas referenceCanvas,
        ScreenAnchorTransform.ScreenAnchor anchor,
        Vector2 anchoredPosition,
        bool respectSafeArea,
        out Vector2 screenPoint)
    {
        screenPoint = default;

        if (referenceCanvas == null)
            return false;

        var canvasRect = referenceCanvas.transform as RectTransform;
        if (canvasRect == null)
            return false;

        var canvasCamera = ResolveCanvasCamera(referenceCanvas);
        var viewport = AnchorToViewport(anchor);

        Vector2 anchorLocal;
        if (respectSafeArea && TrySafeAreaToCanvasLocal(canvasRect, canvasCamera, out var safeMin, out var safeMax))
        {
            anchorLocal = new Vector2(
                Mathf.Lerp(safeMin.x, safeMax.x, viewport.x),
                Mathf.Lerp(safeMin.y, safeMax.y, viewport.y));
        }
        else
        {
            var rect = canvasRect.rect;
            anchorLocal = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, viewport.x),
                Mathf.Lerp(rect.yMin, rect.yMax, viewport.y));
        }

        var canvasWorld = canvasRect.TransformPoint(anchorLocal + anchoredPosition);
        screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, canvasWorld);
        return true;
    }

    static Camera ResolveCanvasCamera(Canvas referenceCanvas)
    {
        if (referenceCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return referenceCanvas.worldCamera != null ? referenceCanvas.worldCamera : Camera.main;
    }

    static bool TrySafeAreaToCanvasLocal(
        RectTransform canvasRect,
        Camera canvasCamera,
        out Vector2 safeMin,
        out Vector2 safeMax)
    {
        var safe = Screen.safeArea;
        if (safe.width <= 0f || safe.height <= 0f)
        {
            safeMin = default;
            safeMax = default;
            return false;
        }

        var bottomLeft = new Vector2(safe.xMin, safe.yMin);
        var topRight = new Vector2(safe.xMax, safe.yMax);
        var hasMin = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            bottomLeft,
            canvasCamera,
            out safeMin);
        var hasMax = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            topRight,
            canvasCamera,
            out safeMax);

        return hasMin && hasMax;
    }
}
