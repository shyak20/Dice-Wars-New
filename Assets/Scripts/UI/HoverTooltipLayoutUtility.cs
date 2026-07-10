using UnityEngine;

/// <summary>
/// Shared hover-tooltip placement: anchor-relative offsets in canvas parent local space
/// (scales with <see cref="Canvas"/> / Canvas Scaler; resolution-independent vs the trigger rect).
/// </summary>
public static class HoverTooltipLayoutUtility
{
    /// <summary>
    /// Places <paramref name="panelRect"/>'s pivot at the reference rect center plus <paramref name="localOffset"/>
    /// in the panel parent's local coordinates.
    /// </summary>
    public static void AlignPanelPivotToRectCenterWithLocalOffset(
        RectTransform panelRect,
        RectTransform reference,
        Vector2 localOffset)
    {
        if (panelRect == null || reference == null)
            return;

        var parentRect = panelRect.parent as RectTransform;
        if (parentRect == null)
            return;

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);
        var centerWorld = (corners[0] + corners[2]) * 0.5f;

        if (!TryWorldPointToParentLocal(parentRect, centerWorld, out var anchorLocal))
            return;

        var localPos = panelRect.localPosition;
        localPos.x = anchorLocal.x + localOffset.x;
        localPos.y = anchorLocal.y + localOffset.y;
        panelRect.localPosition = localPos;
    }

    /// <summary>
    /// Places panel pivot at a world point (e.g. top-center of a rect) plus local offset.
    /// </summary>
    public static void AlignPanelPivotToWorldPointWithLocalOffset(
        RectTransform panelRect,
        Vector3 worldPoint,
        Vector2 localOffset)
    {
        if (panelRect == null)
            return;

        var parentRect = panelRect.parent as RectTransform;
        if (parentRect == null)
            return;

        if (!TryWorldPointToParentLocal(parentRect, worldPoint, out var anchorLocal))
            return;

        var localPos = panelRect.localPosition;
        localPos.x = anchorLocal.x + localOffset.x;
        localPos.y = anchorLocal.y + localOffset.y;
        panelRect.localPosition = localPos;
    }

    /// <summary>
    /// Shifts <paramref name="panelRect"/> left/right so it stays inside the screen (with padding).
    /// Call after content is set and layout is up to date (see <see cref="ForceRebuildLayout"/>).
    /// </summary>
    public static void ClampRectInsideScreenHorizontally(RectTransform panelRect, float screenEdgePadding)
    {
        if (panelRect == null)
            return;

        var canvas = panelRect.GetComponentInParent<Canvas>();
        var cameraForCanvas = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        var corners = new Vector3[4];
        panelRect.GetWorldCorners(corners);
        var minScreen = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, corners[0]);
        var maxScreen = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, corners[2]);

        var shiftX = 0f;
        var leftLimit = screenEdgePadding;
        var rightLimit = Screen.width - screenEdgePadding;

        if (minScreen.x < leftLimit) shiftX = leftLimit - minScreen.x;
        else if (maxScreen.x > rightLimit) shiftX = rightLimit - maxScreen.x;

        if (Mathf.Abs(shiftX) < 0.01f)
            return;

        var panelScreenPos = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, panelRect.position);
        panelScreenPos.x += shiftX;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                panelRect.parent as RectTransform,
                panelScreenPos,
                cameraForCanvas,
                out var clampedWorldPos))
        {
            panelRect.position = clampedWorldPos;
        }
    }

    /// <summary>
    /// Stacks <paramref name="panelRect"/> directly above or below <paramref name="reference"/> (horizontally centered
    /// on it), with <paramref name="gapLocal"/> spacing in the panel parent's local units.
    /// </summary>
    public static void StackPanelAboveOrBelowRect(RectTransform panelRect, RectTransform reference, float gapLocal, bool above)
    {
        if (panelRect == null || reference == null)
            return;

        var parentRect = panelRect.parent as RectTransform;
        if (parentRect == null)
            return;

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);
        var refCenterWorld = (corners[0] + corners[2]) * 0.5f;
        var refEdgeWorld = above ? corners[1] : corners[0]; // top-left or bottom-left

        if (!TryWorldPointToParentLocal(parentRect, refCenterWorld, out var refCenterLocal))
            return;
        if (!TryWorldPointToParentLocal(parentRect, refEdgeWorld, out var refEdgeLocal))
            return;

        var pivot = panelRect.pivot;
        var size = panelRect.rect.size;
        var scale = panelRect.localScale;
        var width = size.x * Mathf.Abs(scale.x);
        var height = size.y * Mathf.Abs(scale.y);

        var localPos = panelRect.localPosition;
        localPos.x = refCenterLocal.x + (pivot.x - 0.5f) * width;
        localPos.y = above
            ? refEdgeLocal.y + gapLocal + pivot.y * height
            : refEdgeLocal.y - gapLocal - (1f - pivot.y) * height;
        panelRect.localPosition = localPos;
    }

    /// <summary>True when the rect's bottom edge is below the screen bottom (with padding).</summary>
    public static bool IsRectBottomOffScreen(RectTransform rect, float padding)
    {
        if (rect == null)
            return false;

        var canvas = rect.GetComponentInParent<Canvas>();
        var cameraForCanvas = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var bottomScreenY = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, corners[0]).y;
        return bottomScreenY < padding;
    }

    /// <summary>Screen-space Y of the rect center (used to pick above/below stacking side).</summary>
    public static float GetRectScreenCenterY(RectTransform rect)
    {
        if (rect == null)
            return 0f;

        var canvas = rect.GetComponentInParent<Canvas>();
        var cameraForCanvas = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var centerWorld = (corners[0] + corners[2]) * 0.5f;
        return RectTransformUtility.WorldToScreenPoint(cameraForCanvas, centerWorld).y;
    }

    /// <summary>Rebuilds layout on the panel so its rect size is valid before measuring/clamping.</summary>
    public static void ForceRebuildLayout(RectTransform panelRect)
    {
        if (panelRect == null)
            return;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    static bool TryWorldPointToParentLocal(RectTransform parentRect, Vector3 worldPoint, out Vector2 localPoint)
    {
        localPoint = default;
        if (parentRect == null)
            return false;

        var canvas = parentRect.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            RectTransformUtility.WorldToScreenPoint(camera, worldPoint),
            camera,
            out localPoint);
    }
}
