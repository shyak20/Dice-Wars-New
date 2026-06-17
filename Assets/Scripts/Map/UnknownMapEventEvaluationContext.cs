using UnityEngine;

/// <summary>Runtime snapshot for evaluating <see cref="UnknownMapEventSO"/> visibility and option rules.</summary>
public readonly struct UnknownMapEventEvaluationContext
{
    public readonly RunManager RunManager;
    public readonly MapGrid Grid;
    public readonly Vector2Int PlayerCell;
    public readonly int MovesTaken;

    public UnknownMapEventEvaluationContext(RunManager runManager, MapGrid grid, Vector2Int playerCell, int movesTaken)
    {
        RunManager = runManager;
        Grid = grid;
        PlayerCell = playerCell;
        MovesTaken = movesTaken;
    }
}
