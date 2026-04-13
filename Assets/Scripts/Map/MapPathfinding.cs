using System.Collections.Generic;
using UnityEngine;

public static class MapPathfinding
{
    /// <summary>BFS on directed exits from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static bool HasPath(MapGrid grid, Vector2Int start, Vector2Int end)
    {
        if (grid == null || !grid.Contains(start) || !grid.Contains(end))
            return false;

        if (start == end)
            return true;

        var visited = new bool[grid.Width * grid.Height];
        var q = new Queue<Vector2Int>();
        q.Enqueue(start);
        visited[start.y * grid.Width + start.x] = true;

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            var tile = grid.Get(p.x, p.y);
            for (var di = 0; di < 4; di++)
            {
                var d = (MapCardinalDirection)di;
                if (!tile.exitMask.Contains(d))
                    continue;
                var n = p + d.ToDelta();
                if (!grid.Contains(n))
                    continue;
                if (n == end)
                    return true;
                var ni = n.y * grid.Width + n.x;
                if (visited[ni])
                    continue;
                visited[ni] = true;
                q.Enqueue(n);
            }
        }

        return false;
    }

    /// <summary>All cells reachable from <paramref name="start"/> following directed exits.</summary>
    public static HashSet<Vector2Int> ReachableFromStart(MapGrid grid, Vector2Int start)
    {
        var set = new HashSet<Vector2Int>();
        if (grid == null || !grid.Contains(start))
            return set;

        var q = new Queue<Vector2Int>();
        q.Enqueue(start);
        set.Add(start);

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            var tile = grid.Get(p.x, p.y);
            for (var di = 0; di < 4; di++)
            {
                var d = (MapCardinalDirection)di;
                if (!tile.exitMask.Contains(d))
                    continue;
                var n = p + d.ToDelta();
                if (!grid.Contains(n) || set.Contains(n))
                    continue;
                set.Add(n);
                q.Enqueue(n);
            }
        }

        return set;
    }

    /// <summary>
    /// True when every cell except <paramref name="end"/> is on some directed path from <paramref name="start"/>.
    /// The end (boss) tile is excluded so its sink state (no outgoing exits) is not part of this check.
    /// </summary>
    public static bool AllNonEndCellsReachableFromStart(MapGrid grid, Vector2Int start, Vector2Int end)
    {
        if (grid == null || !grid.Contains(start) || !grid.Contains(end))
            return false;

        var reachable = ReachableFromStart(grid, start);
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (p == end)
                    continue;
                if (!reachable.Contains(p))
                    return false;
            }
        }

        return true;
    }
}
