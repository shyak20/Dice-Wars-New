using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Arranges children in a dynamic pyramid:
/// 1 -> [1]
/// 2 -> [2]
/// 3 -> [2,1]
/// 4 -> [3,1]
/// 5 -> [3,2]
/// 6 -> [3,2,1]
/// ... then continues with wider bottom floors.
/// </summary>
public sealed class DynamicPyramidLayoutGroup : LayoutGroup
{
    private enum HorizontalRowAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    private enum VerticalBuildDirection
    {
        Upward = 0,
        Downward = 1
    }

    [Header("Cell")]
    [SerializeField] private Vector2 cellSize = new Vector2(120f, 120f);
    [SerializeField] private bool useChildPreferredSize = true;

    [Header("Spacing")]
    [SerializeField, Min(0f)] private float horizontalSpacing = 12f;
    [SerializeField, Min(0f)] private float verticalSpacing = 12f;

    [Header("Vertical Build")]
    [Tooltip("Upward keeps the first floor anchored and builds higher floors above it. Downward builds new floors below the first floor.")]
    [SerializeField] private VerticalBuildDirection verticalBuildDirection = VerticalBuildDirection.Upward;

    [Header("Horizontal Alignment")]
    [Tooltip("How each floor aligns inside the pyramid base width.")]
    [SerializeField] private HorizontalRowAlignment horizontalRowAlignment = HorizontalRowAlignment.Center;

    private readonly List<int> _rowCountsFromBottom = new List<int>();
    private float _cachedCellWidth;
    private float _cachedCellHeight;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        CacheCellSize();
        BuildRows(rectChildren.Count);

        float preferredWidth = GetMaxRowCount() * _cachedCellWidth;
        if (GetMaxRowCount() > 1)
            preferredWidth += (GetMaxRowCount() - 1) * horizontalSpacing;

        preferredWidth += padding.horizontal;
        SetLayoutInputForAxis(preferredWidth, preferredWidth, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        CacheCellSize();
        BuildRows(rectChildren.Count);

        float preferredHeight = _rowCountsFromBottom.Count * _cachedCellHeight;
        if (_rowCountsFromBottom.Count > 1)
            preferredHeight += (_rowCountsFromBottom.Count - 1) * verticalSpacing;

        preferredHeight += padding.vertical;
        SetLayoutInputForAxis(preferredHeight, preferredHeight, -1f, 1);
    }

    public override void SetLayoutHorizontal()
    {
        SetChildrenAlongAxes();
    }

    public override void SetLayoutVertical()
    {
        SetChildrenAlongAxes();
    }

    private void SetChildrenAlongAxes()
    {
        CacheCellSize();
        BuildRows(rectChildren.Count);

        if (rectChildren.Count == 0 || _rowCountsFromBottom.Count == 0)
            return;

        float availableWidth = rectTransform.rect.width - padding.horizontal;
        float maxRowWidth = GetMaxRowWidth();
        float pyramidLeft = padding.left + GetHorizontalGroupOffset(availableWidth, maxRowWidth);
        float firstFloorTop = GetFirstFloorTop();

        int childIndex = 0;
        int rowCount = _rowCountsFromBottom.Count;
        for (int rowFromBottom = 0; rowFromBottom < rowCount; rowFromBottom++)
        {
            int childrenInRow = _rowCountsFromBottom[rowFromBottom];
            float rowWidth = childrenInRow * _cachedCellWidth;
            if (childrenInRow > 1)
                rowWidth += (childrenInRow - 1) * horizontalSpacing;

            float rowLeft = pyramidLeft + GetHorizontalGroupOffset(maxRowWidth, rowWidth);
            float rowTop = verticalBuildDirection == VerticalBuildDirection.Upward
                ? firstFloorTop - rowFromBottom * (_cachedCellHeight + verticalSpacing)
                : firstFloorTop + rowFromBottom * (_cachedCellHeight + verticalSpacing);

            for (int col = 0; col < childrenInRow && childIndex < rectChildren.Count; col++)
            {
                RectTransform child = rectChildren[childIndex++];
                float x = rowLeft + col * (_cachedCellWidth + horizontalSpacing);
                SetChildAlongAxis(child, 0, x, _cachedCellWidth);
                SetChildAlongAxis(child, 1, rowTop, _cachedCellHeight);
            }
        }
    }

    private void CacheCellSize()
    {
        if (!useChildPreferredSize || rectChildren.Count == 0)
        {
            _cachedCellWidth = cellSize.x;
            _cachedCellHeight = cellSize.y;
            return;
        }

        float maxW = 0f;
        float maxH = 0f;
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            maxW = Mathf.Max(maxW, LayoutUtility.GetPreferredSize(child, 0));
            maxH = Mathf.Max(maxH, LayoutUtility.GetPreferredSize(child, 1));
        }

        _cachedCellWidth = maxW > 0f ? maxW : cellSize.x;
        _cachedCellHeight = maxH > 0f ? maxH : cellSize.y;
    }

    private void BuildRows(int childCount)
    {
        _rowCountsFromBottom.Clear();
        if (childCount <= 0)
            return;

        int bottomCapacity = 1;
        while (bottomCapacity * (bottomCapacity + 1) / 2 < childCount)
            bottomCapacity++;

        int remaining = childCount;
        for (int cap = bottomCapacity; cap >= 1 && remaining > 0; cap--)
        {
            int countOnThisRow = Mathf.Min(cap, remaining);
            _rowCountsFromBottom.Add(countOnThisRow);
            remaining -= countOnThisRow;
        }
    }

    private int GetMaxRowCount()
    {
        int max = 0;
        for (int i = 0; i < _rowCountsFromBottom.Count; i++)
            max = Mathf.Max(max, _rowCountsFromBottom[i]);
        return max;
    }

    private float GetFirstFloorTop()
    {
        if (verticalBuildDirection == VerticalBuildDirection.Upward)
            return rectTransform.rect.height - padding.bottom - _cachedCellHeight;

        return padding.top;
    }

    private float GetMaxRowWidth()
    {
        int maxRowCount = GetMaxRowCount();
        if (maxRowCount <= 0)
            return 0f;

        float width = maxRowCount * _cachedCellWidth;
        if (maxRowCount > 1)
            width += (maxRowCount - 1) * horizontalSpacing;
        return width;
    }

    private float GetHorizontalGroupOffset(float containerWidth, float contentWidth)
    {
        float extra = Mathf.Max(0f, containerWidth - contentWidth);
        switch (horizontalRowAlignment)
        {
            case HorizontalRowAlignment.Left:
                return 0f;
            case HorizontalRowAlignment.Right:
                return extra;
            case HorizontalRowAlignment.Center:
            default:
                return extra * 0.5f;
        }
    }
}
