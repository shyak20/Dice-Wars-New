using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Places active children evenly on a circle. Alignment fans children from the start angle
/// (+ / - / centered), with optional child Z rotation and shrink-to-fit.
/// </summary>
[AddComponentMenu("Layout/Circular Layout Group")]
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class CircularLayoutGroup : LayoutGroup
{
    public enum ElementAlignment
    {
        /// <summary>Element 0 at start angle; later elements advance in +degrees.</summary>
        Left = 0,
        /// <summary>Occupied elements are centered on the start angle.</summary>
        Center = 1,
        /// <summary>Element 0 at start angle; later elements advance in -degrees.</summary>
        Right = 2
    }

    [Header("Circle")]
    [SerializeField, Min(0f)] private float circleRadius = 100f;
    [Tooltip("Angle of element 0 / the alignment origin in degrees. 0 = right, increases counter-clockwise.")]
    [SerializeField] private float startAngleDegrees;
    [Tooltip("Left: later elements go +degrees from element 0. Right: later elements go -degrees. Center: the occupied set is centered on Start Angle.")]
    [SerializeField] private ElementAlignment alignment = ElementAlignment.Center;
    [Tooltip("When on, spacing uses the current child count around 360°. When off, spacing uses Desired Element Count and each child keeps its index slot.")]
    [SerializeField] private bool fillFullCircle = true;
    [Tooltip("Slot count for angular spacing when Fill Full Circle is off. Step = 360 / Desired Element Count.")]
    [SerializeField, Min(1)] private int desiredElementCount = 4;

    [Header("Child Rotation")]
    [Tooltip("When on, each child Z rotation matches its place on the circle plus Element Rotation Degrees.")]
    [SerializeField] private bool rotateElements;
    [Tooltip("Extra Z degrees applied when Rotate Elements is on (e.g. -90 to face outward).")]
    [SerializeField] private float elementRotationDegrees;

    [Header("Sizing")]
    [Tooltip("When on, all children use Element Width / Height as their base size before Shrink To Fit.")]
    [SerializeField] private bool overrideElementSize;
    [SerializeField, Min(0f)] private float elementWidth = 100f;
    [SerializeField, Min(0f)] private float elementHeight = 100f;
    [Tooltip("When on, uniformly shrink children if their size would overlap on the ring.")]
    [SerializeField] private bool shrinkToFit;

    [Header("Gizmo")]
    [SerializeField] private bool showCircleGizmo = true;
    [SerializeField] private Color circleGizmoColor = new Color(0.2f, 0.85f, 1f, 0.9f);

    public float CircleRadius
    {
        get => circleRadius;
        set => SetProperty(ref circleRadius, Mathf.Max(0f, value));
    }

    public float StartAngleDegrees
    {
        get => startAngleDegrees;
        set => SetProperty(ref startAngleDegrees, value);
    }

    public ElementAlignment Alignment
    {
        get => alignment;
        set => SetProperty(ref alignment, value);
    }

    public bool FillFullCircle
    {
        get => fillFullCircle;
        set => SetProperty(ref fillFullCircle, value);
    }

    public int DesiredElementCount
    {
        get => desiredElementCount;
        set => SetProperty(ref desiredElementCount, Mathf.Max(1, value));
    }

    public bool RotateElements
    {
        get => rotateElements;
        set => SetProperty(ref rotateElements, value);
    }

    public float ElementRotationDegrees
    {
        get => elementRotationDegrees;
        set => SetProperty(ref elementRotationDegrees, value);
    }

    public bool OverrideElementSize
    {
        get => overrideElementSize;
        set => SetProperty(ref overrideElementSize, value);
    }

    public float ElementWidth
    {
        get => elementWidth;
        set => SetProperty(ref elementWidth, Mathf.Max(0f, value));
    }

    public float ElementHeight
    {
        get => elementHeight;
        set => SetProperty(ref elementHeight, Mathf.Max(0f, value));
    }

    public bool ShrinkToFit
    {
        get => shrinkToFit;
        set => SetProperty(ref shrinkToFit, value);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        circleRadius = Mathf.Max(0f, circleRadius);
        elementWidth = Mathf.Max(0f, elementWidth);
        elementHeight = Mathf.Max(0f, elementHeight);
        desiredElementCount = Mathf.Max(1, desiredElementCount);
    }

    void OnDrawGizmos()
    {
        if (!showCircleGizmo)
            return;

        DrawCircleGizmo(selected: false);
    }

    void OnDrawGizmosSelected()
    {
        if (!showCircleGizmo)
            return;

        DrawCircleGizmo(selected: true);
    }

    void DrawCircleGizmo(bool selected)
    {
        if (rectTransform == null)
            return;

        GetCircleLayoutCenterFromTopLeft(out var centerX, out var centerYFromTop);
        var localCenter = LayoutTopLeftToLocal(centerX, centerYFromTop);
        var worldCenter = rectTransform.TransformPoint(localCenter);

        var localRadiusPoint = LayoutTopLeftToLocal(centerX + circleRadius, centerYFromTop);
        var worldRadiusPoint = rectTransform.TransformPoint(localRadiusPoint);
        var worldRadius = Vector3.Distance(worldCenter, worldRadiusPoint);

        var color = circleGizmoColor;
        if (!selected)
            color.a *= 0.55f;

        Handles.color = color;
        Handles.DrawWireDisc(worldCenter, rectTransform.forward, worldRadius);

        var childCount = Mathf.Max(0, rectChildren.Count);
        var slotCount = GetAngularSlotCount(Mathf.Max(1, childCount));
        var markerCount = fillFullCircle ? Mathf.Max(1, childCount) : slotCount;
        for (var i = 0; i < markerCount; i++)
        {
            var angleDegrees = GetChildAngleDegrees(i, Mathf.Max(1, childCount), slotCount);
            var angleRadians = angleDegrees * Mathf.Deg2Rad;
            var markerLocal = LayoutTopLeftToLocal(
                centerX + (Mathf.Cos(angleRadians) * circleRadius),
                centerYFromTop - (Mathf.Sin(angleRadians) * circleRadius));
            var worldMarker = rectTransform.TransformPoint(markerLocal);
            if (i == 0)
                Handles.DrawLine(worldCenter, worldMarker);

            Handles.DotHandleCap(
                0,
                worldMarker,
                Quaternion.identity,
                HandleUtility.GetHandleSize(worldCenter) * (i == 0 ? 0.035f : 0.025f),
                EventType.Repaint);
        }

        Handles.DotHandleCap(
            0,
            worldCenter,
            Quaternion.identity,
            HandleUtility.GetHandleSize(worldCenter) * 0.04f,
            EventType.Repaint);
    }
#endif

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        GetMaxChildBaseSize(out var maxW, out _);
        var preferred = padding.horizontal + (circleRadius * 2f) + maxW;
        SetLayoutInputForAxis(preferred, preferred, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        GetMaxChildBaseSize(out _, out var maxH);
        var preferred = padding.vertical + (circleRadius * 2f) + maxH;
        SetLayoutInputForAxis(preferred, preferred, -1f, 1);
    }

    public override void SetLayoutHorizontal()
    {
        SetChildrenOnCircle();
    }

    public override void SetLayoutVertical()
    {
        SetChildrenOnCircle();
    }

    void SetChildrenOnCircle()
    {
        var childCount = rectChildren.Count;
        if (childCount == 0)
            return;

        GetMaxChildBaseSize(out var maxBaseW, out var maxBaseH);
        var slotCount = GetAngularSlotCount(childCount);
        var scale = ComputeShrinkScale(slotCount, maxBaseW, maxBaseH);
        GetCircleLayoutCenterFromTopLeft(out var centerX, out var centerYFromTop);

        for (var i = 0; i < childCount; i++)
        {
            var child = rectChildren[i];
            GetChildBaseSize(child, out var baseW, out var baseH);

            var width = baseW * scale;
            var height = baseH * scale;

            var angleDegrees = GetChildAngleDegrees(i, childCount, slotCount);
            var angleRadians = angleDegrees * Mathf.Deg2Rad;
            var childCenterX = centerX + (Mathf.Cos(angleRadians) * circleRadius);
            var childCenterYFromTop = centerYFromTop - (Mathf.Sin(angleRadians) * circleRadius);

            SetChildAlongAxis(child, 0, childCenterX - (width * 0.5f), width);
            SetChildAlongAxis(child, 1, childCenterYFromTop - (height * 0.5f), height);

            if (rotateElements)
                child.localEulerAngles = new Vector3(0f, 0f, angleDegrees + elementRotationDegrees);
            else
                child.localEulerAngles = Vector3.zero;
        }
    }

    int GetAngularSlotCount(int childCount)
    {
        if (fillFullCircle)
            return Mathf.Max(1, childCount);

        return Mathf.Max(1, desiredElementCount);
    }

    float GetAngleStepMagnitudeDegrees(int slotCount)
    {
        if (slotCount <= 1)
            return 0f;

        return 360f / slotCount;
    }

    /// <summary>
    /// Left: element 0 at start, later indices in +degrees.
    /// Right: element 0 at start, later indices in -degrees.
    /// Center: occupied children centered on start angle.
    /// </summary>
    float GetChildAngleDegrees(int childIndex, int childCount, int slotCount)
    {
        var step = GetAngleStepMagnitudeDegrees(slotCount);
        switch (alignment)
        {
            case ElementAlignment.Left:
                return startAngleDegrees + (childIndex * step);
            case ElementAlignment.Right:
                return startAngleDegrees - (childIndex * step);
            case ElementAlignment.Center:
            default:
            {
                var occupied = Mathf.Max(1, childCount);
                var firstOffset = -((occupied - 1) * 0.5f) * step;
                return startAngleDegrees + firstOffset + (childIndex * step);
            }
        }
    }

    void GetCircleLayoutCenterFromTopLeft(out float centerX, out float centerYFromTop)
    {
        var availableWidth = rectTransform.rect.width - padding.horizontal;
        var availableHeight = rectTransform.rect.height - padding.vertical;
        centerX = padding.left + (availableWidth * 0.5f);
        centerYFromTop = padding.top + (availableHeight * 0.5f);
    }

    Vector3 LayoutTopLeftToLocal(float xFromLeft, float yFromTop)
    {
        var rect = rectTransform.rect;
        return new Vector3(rect.xMin + xFromLeft, rect.yMax - yFromTop, 0f);
    }

    float ComputeShrinkScale(int slotCount, float maxBaseW, float maxBaseH)
    {
        if (!shrinkToFit || slotCount < 2)
            return 1f;

        var preferredExtent = Mathf.Max(maxBaseW, maxBaseH);
        if (preferredExtent <= 0f)
            return 1f;

        var chord = 2f * circleRadius * Mathf.Sin(Mathf.PI / slotCount);
        if (chord <= 0f)
            return 1f;

        return Mathf.Clamp01(chord / preferredExtent);
    }

    void GetMaxChildBaseSize(out float maxWidth, out float maxHeight)
    {
        if (overrideElementSize)
        {
            maxWidth = elementWidth;
            maxHeight = elementHeight;
            return;
        }

        maxWidth = 0f;
        maxHeight = 0f;
        for (var i = 0; i < rectChildren.Count; i++)
        {
            GetChildBaseSize(rectChildren[i], out var w, out var h);
            maxWidth = Mathf.Max(maxWidth, w);
            maxHeight = Mathf.Max(maxHeight, h);
        }
    }

    void GetChildBaseSize(RectTransform child, out float width, out float height)
    {
        if (overrideElementSize)
        {
            width = elementWidth;
            height = elementHeight;
            return;
        }

        width = Mathf.Max(0f, LayoutUtility.GetPreferredSize(child, 0));
        height = Mathf.Max(0f, LayoutUtility.GetPreferredSize(child, 1));
        if (width <= 0f)
            width = child.rect.width;
        if (height <= 0f)
            height = child.rect.height;
    }
}
