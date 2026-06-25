using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grid layout matching <see cref="GridLayoutGroup"/> with optional per-cell
/// <see cref="CellChildAlignment.UpperCenter"/> / <see cref="CellChildAlignment.LowerCenter"/> placement,
/// and <see cref="Corner.UpperCenter"/> / <see cref="Corner.LowerCenter"/> start corners that center the grid horizontally.
/// </summary>
[AddComponentMenu("Layout/Aligned Grid Layout Group")]
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class AlignedGridLayoutGroup : LayoutGroup
{
    public enum Corner
    {
        UpperLeft = 0,
        UpperRight = 1,
        LowerLeft = 2,
        LowerRight = 3,
        UpperCenter = 4,
        LowerCenter = 5,
    }

    public enum Axis
    {
        Horizontal = 0,
        Vertical = 1,
    }

    public enum Constraint
    {
        Flexible = 0,
        FixedColumnCount = 1,
        FixedRowCount = 2,
    }

    public enum CellChildAlignment
    {
        /// <summary>Child fills the entire cell (default <see cref="GridLayoutGroup"/> behavior).</summary>
        Stretch = 0,
        UpperCenter = 1,
        LowerCenter = 2,
    }

    [SerializeField] private Corner startCorner = Corner.UpperLeft;
    [SerializeField] private Axis startAxis = Axis.Horizontal;
    [SerializeField] private Vector2 cellSize = new Vector2(100f, 100f);
    [SerializeField] private Vector2 spacing = Vector2.zero;
    [SerializeField] private Constraint constraint = Constraint.Flexible;
    [SerializeField] private int constraintCount = 2;
    [SerializeField] private CellChildAlignment cellChildAlignment = CellChildAlignment.Stretch;

    public Corner StartCorner
    {
        get => startCorner;
        set => SetProperty(ref startCorner, value);
    }

    public Axis StartAxis
    {
        get => startAxis;
        set => SetProperty(ref startAxis, value);
    }

    public Vector2 CellSize
    {
        get => cellSize;
        set => SetProperty(ref cellSize, value);
    }

    public Vector2 Spacing
    {
        get => spacing;
        set => SetProperty(ref spacing, value);
    }

    public Constraint LayoutConstraint
    {
        get => constraint;
        set => SetProperty(ref constraint, value);
    }

    public int ConstraintCount
    {
        get => constraintCount;
        set => SetProperty(ref constraintCount, Mathf.Max(1, value));
    }

    public CellChildAlignment ChildAlignmentInCell
    {
        get => cellChildAlignment;
        set => SetProperty(ref cellChildAlignment, value);
    }

    public int GeneratedRowCount { get; private set; }
    public int GeneratedColumnCount { get; private set; }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        constraintCount = Mathf.Max(1, constraintCount);
    }
#endif

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();

        var cellWidthWithSpacing = cellSize.x + spacing.x;
        float totalMin;
        float totalPreferred;

        if (constraint == Constraint.FixedColumnCount)
        {
            totalMin = totalPreferred = padding.horizontal + cellWidthWithSpacing * constraintCount - spacing.x;
        }
        else if (constraint == Constraint.FixedRowCount)
        {
            var columns = Mathf.CeilToInt(rectChildren.Count / (float)constraintCount - 0.001f);
            totalMin = totalPreferred = padding.horizontal + cellWidthWithSpacing * columns - spacing.x;
        }
        else
        {
            totalMin = padding.horizontal + cellWidthWithSpacing - spacing.x;
            var preferredColumnCount = Mathf.CeilToInt(Mathf.Sqrt(rectChildren.Count));
            totalPreferred = padding.horizontal + cellWidthWithSpacing * preferredColumnCount - spacing.x;
        }

        SetLayoutInputForAxis(totalMin, totalPreferred, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        var cellHeightWithSpacing = cellSize.y + spacing.y;
        float totalMin;
        float totalPreferred;

        if (constraint == Constraint.FixedColumnCount)
        {
            var rows = Mathf.CeilToInt(rectChildren.Count / (float)constraintCount - 0.001f);
            totalMin = totalPreferred = padding.vertical + cellHeightWithSpacing * rows - spacing.y;
        }
        else if (constraint == Constraint.FixedRowCount)
        {
            totalMin = totalPreferred = padding.vertical + cellHeightWithSpacing * constraintCount - spacing.y;
        }
        else
        {
            totalMin = padding.vertical + cellHeightWithSpacing - spacing.y;

            var usableWidth = rectTransform.rect.width - padding.horizontal + spacing.x + 0.001f;
            var cellWidthWithSpacing = cellSize.x + spacing.x;
            var cellCountX = Mathf.Max(1, Mathf.FloorToInt(usableWidth / cellWidthWithSpacing));
            var rowCount = Mathf.CeilToInt(rectChildren.Count / (float)cellCountX);
            totalPreferred = padding.vertical + cellHeightWithSpacing * rowCount - spacing.y;
        }

        SetLayoutInputForAxis(totalMin, totalPreferred, -1f, 1);
    }

    public override void SetLayoutHorizontal()
    {
        GeneratedRowCount = 0;
        GeneratedColumnCount = 0;
        SetCellsAlongAxis(0);
    }

    public override void SetLayoutVertical()
    {
        SetCellsAlongAxis(1);
    }

    void SetCellsAlongAxis(int axis)
    {
        var childCount = rectChildren.Count;
        if (axis == 0)
        {
            for (var i = 0; i < childCount; i++)
            {
                var rect = rectChildren[i];
                m_Tracker.Add(this, rect,
                    DrivenTransformProperties.Anchors |
                    DrivenTransformProperties.AnchoredPosition |
                    DrivenTransformProperties.SizeDelta);

                rect.anchorMin = Vector2.up;
                rect.anchorMax = Vector2.up;

                if (cellChildAlignment == CellChildAlignment.Stretch)
                    rect.sizeDelta = cellSize;
            }

            return;
        }

        var width = rectTransform.rect.size.x;
        var height = rectTransform.rect.size.y;

        var cellCountX = 1;
        var cellCountY = 1;
        if (constraint == Constraint.FixedColumnCount)
        {
            cellCountX = constraintCount;
            if (childCount > cellCountX)
                cellCountY = childCount / cellCountX + (childCount % cellCountX > 0 ? 1 : 0);
        }
        else if (constraint == Constraint.FixedRowCount)
        {
            cellCountY = constraintCount;
            if (childCount > cellCountY)
                cellCountX = childCount / cellCountY + (childCount % cellCountY > 0 ? 1 : 0);
        }
        else
        {
            cellCountX = cellSize.x + spacing.x <= 0f
                ? int.MaxValue
                : Mathf.Max(1, Mathf.FloorToInt((width - padding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x)));
            cellCountY = cellSize.y + spacing.y <= 0f
                ? int.MaxValue
                : Mathf.Max(1, Mathf.FloorToInt((height - padding.vertical + spacing.y + 0.001f) / (cellSize.y + spacing.y)));
        }

        var cornerX = (int)startCorner % 2;
        var cornerY = (int)startCorner / 2;
        var centerGridHorizontally = startCorner == Corner.UpperCenter || startCorner == Corner.LowerCenter;
        if (startCorner == Corner.UpperCenter)
            cornerY = 0;
        else if (startCorner == Corner.LowerCenter)
            cornerY = 1;

        int cellsPerMainAxis;
        int actualCellCountX;
        int actualCellCountY;
        if (startAxis == Axis.Horizontal)
        {
            cellsPerMainAxis = cellCountX;
            actualCellCountX = Mathf.Clamp(cellCountX, 1, childCount);
            actualCellCountY = constraint == Constraint.FixedRowCount
                ? Mathf.Min(cellCountY, childCount)
                : Mathf.Clamp(cellCountY, 1, Mathf.CeilToInt(childCount / (float)cellsPerMainAxis));
        }
        else
        {
            cellsPerMainAxis = cellCountY;
            actualCellCountY = Mathf.Clamp(cellCountY, 1, childCount);
            actualCellCountX = constraint == Constraint.FixedColumnCount
                ? Mathf.Min(cellCountX, childCount)
                : Mathf.Clamp(cellCountX, 1, Mathf.CeilToInt(childCount / (float)cellsPerMainAxis));
        }

        var requiredSpace = new Vector2(
            actualCellCountX * cellSize.x + (actualCellCountX - 1) * spacing.x,
            actualCellCountY * cellSize.y + (actualCellCountY - 1) * spacing.y);
        var startOffset = new Vector2(
            GetStartOffset(0, requiredSpace.x),
            GetStartOffset(1, requiredSpace.y));

        var childrenToMove = 0;
        if (childCount > constraintCount
            && Mathf.CeilToInt(childCount / (float)cellsPerMainAxis) < constraintCount)
        {
            childrenToMove = constraintCount - Mathf.CeilToInt(childCount / (float)cellsPerMainAxis);
            childrenToMove += Mathf.FloorToInt(childrenToMove / (cellsPerMainAxis - 1f));
            if (childCount % cellsPerMainAxis == 1)
                childrenToMove += 1;
        }

        for (var i = 0; i < childCount; i++)
        {
            int rawPositionX;
            int rawPositionY;
            if (startAxis == Axis.Horizontal)
            {
                if (constraint == Constraint.FixedRowCount && childCount - i <= childrenToMove)
                {
                    rawPositionX = 0;
                    rawPositionY = constraintCount - (childCount - i);
                }
                else
                {
                    rawPositionX = i % cellsPerMainAxis;
                    rawPositionY = i / cellsPerMainAxis;
                }
            }
            else
            {
                if (constraint == Constraint.FixedColumnCount && childCount - i <= childrenToMove)
                {
                    rawPositionX = constraintCount - (childCount - i);
                    rawPositionY = 0;
                }
                else
                {
                    rawPositionX = i / cellsPerMainAxis;
                    rawPositionY = i % cellsPerMainAxis;
                }
            }

            var positionX = cornerX == 1 && !centerGridHorizontally
                ? actualCellCountX - 1 - rawPositionX
                : rawPositionX;
            var positionY = cornerY == 1
                ? actualCellCountY - 1 - rawPositionY
                : rawPositionY;

            var cellX = centerGridHorizontally && startAxis == Axis.Horizontal
                ? GetCenteredRowCellX(width, cellsPerMainAxis, rawPositionX, rawPositionY)
                : startOffset.x + (cellSize.x + spacing.x) * positionX;
            var cellY = centerGridHorizontally && startAxis == Axis.Vertical
                ? GetCenteredColumnCellY(height, cellsPerMainAxis, rawPositionY, rawPositionX)
                : startOffset.y + (cellSize.y + spacing.y) * positionY;
            PlaceChildInCell(rectChildren[i], cellX, cellY, cellSize.x, cellSize.y);
        }

        GeneratedRowCount = actualCellCountY;
        GeneratedColumnCount = actualCellCountX;
    }

    float GetCenteredRowCellX(float parentWidth, int cellsPerMainAxis, int columnInRow, int rowIndex)
    {
        var rowStartIndex = rowIndex * cellsPerMainAxis;
        var cellsInRow = Mathf.Min(cellsPerMainAxis, rectChildren.Count - rowStartIndex);
        var rowWidth = cellsInRow * cellSize.x + Mathf.Max(0, cellsInRow - 1) * spacing.x;
        var rowStartX = padding.left + (parentWidth - padding.horizontal - rowWidth) * 0.5f;
        return rowStartX + (cellSize.x + spacing.x) * columnInRow;
    }

    float GetCenteredColumnCellY(float parentHeight, int cellsPerMainAxis, int rowInColumn, int columnIndex)
    {
        var columnStartIndex = columnIndex * cellsPerMainAxis;
        var cellsInColumn = Mathf.Min(cellsPerMainAxis, rectChildren.Count - columnStartIndex);
        var columnHeight = cellsInColumn * cellSize.y + Mathf.Max(0, cellsInColumn - 1) * spacing.y;
        var columnStartY = padding.top + (parentHeight - padding.vertical - columnHeight) * 0.5f;
        return columnStartY + (cellSize.y + spacing.y) * rowInColumn;
    }

    void PlaceChildInCell(RectTransform child, float cellX, float cellY, float cellW, float cellH)
    {
        if (cellChildAlignment == CellChildAlignment.Stretch)
        {
            SetChildAlongAxis(child, 0, cellX, cellW);
            SetChildAlongAxis(child, 1, cellY, cellH);
            return;
        }

        var childW = Mathf.Min(LayoutUtility.GetPreferredWidth(child), cellW);
        var childH = Mathf.Min(LayoutUtility.GetPreferredHeight(child), cellH);
        if (childW <= 0f)
            childW = cellW;
        if (childH <= 0f)
            childH = cellH;

        var pivot = child.pivot;
        var posX = cellX + (cellW - childW) * 0.5f - childW * (pivot.x - 0.5f);
        var posY = cellChildAlignment == CellChildAlignment.UpperCenter
            ? cellY - childH * (1f - pivot.y)
            : cellY + cellH - childH * pivot.y;

        SetChildAlongAxis(child, 0, posX, childW);
        SetChildAlongAxis(child, 1, posY, childH);
    }
}
