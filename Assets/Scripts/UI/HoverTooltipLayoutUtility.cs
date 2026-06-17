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
