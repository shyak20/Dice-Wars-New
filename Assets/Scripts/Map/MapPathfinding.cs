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

    /// <summary>Directed BFS step count from <paramref name="start"/> along tile exits; unreachable cells stay -1.</summary>
    public static int[] ComputeDirectedStepsFromStart(MapGrid grid, Vector2Int start)
    {
        var w = grid.Width;
        var h = grid.Height;
        var len = w * h;
        var dist = new int[len];
        for (var i = 0; i < len; i++)
            dist[i] = -1;

        if (grid == null || !grid.Contains(start))
            return dist;

        var q = new Queue<Vector2Int>();
        dist[start.y * w + start.x] = 0;
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            var baseD = dist[p.y * w + p.x];
            var tile = grid.Get(p.x, p.y);
            for (var di = 0; di < 4; di++)
            {
                var dir = (MapCardinalDirection)di;
                if (!tile.exitMask.Contains(dir))
                    continue;
                var n = p + dir.ToDelta();
                if (!grid.Contains(n))
                    continue;
                var ni = n.y * w + n.x;
                if (dist[ni] >= 0)
                    continue;
                dist[ni] = baseD + 1;
                q.Enqueue(n);
            }
        }

        return dist;
    }

    public static int GetDirectedSteps(int[] distFromStart, int width, Vector2Int pos)
    {
        if (distFromStart == null || width < 1 || pos.x < 0 || pos.y < 0)
            return -1;
        var i = pos.y * width + pos.x;
        if (i < 0 || i >= distFromStart.Length)
            return -1;
        return distFromStart[i];
    }

    /// <summary>Visited tiles and empty (<see cref="MapEventType.None"/>) tiles the player may move through.</summary>
    public static bool IsTraversableIntermediateTile(MapTile tile) =>
        tile.eventConsumed || tile.eventType == MapEventType.None;

    /// <summary>
    /// True when the player may move through <paramref name="cell"/> on the way to <paramref name="destination"/>.
    /// The destination itself is always allowed; earlier hops must be traversable intermediates.
    /// </summary>
    public static bool IsPassableIntermediateTile(MapTile tile, Vector2Int cell, Vector2Int destination)
    {
        if (cell == destination)
            return true;

        return IsTraversableIntermediateTile(tile);
    }

    /// <summary>
    /// All cells reachable from <paramref name="from"/> following directed exits through visited or empty intermediates.
    /// Does not include <paramref name="from"/>.
    /// </summary>
    public static void CollectReachableMoveTargets(MapGrid grid, Vector2Int from, HashSet<Vector2Int> results)
    {
        results?.Clear();
        if (results == null || grid == null || !grid.Contains(from))
            return;

        var visited = new HashSet<Vector2Int> { from };
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);

        while (q.Count > 0)
        {
            var current = q.Dequeue();
            var tile = grid.Get(current.x, current.y);
            for (var di = 0; di < 4; di++)
            {
                var dir = (MapCardinalDirection)di;
                if (!tile.exitMask.Contains(dir))
                    continue;

                var next = current + dir.ToDelta();
                if (!grid.Contains(next) || !visited.Add(next))
                    continue;

                results.Add(next);

                var nextTile = grid.Get(next.x, next.y);
                if (IsTraversableIntermediateTile(nextTile))
                    q.Enqueue(next);
            }
        }
    }

    /// <summary>
    /// Builds a directed path from <paramref name="from"/> to <paramref name="to"/> following tile exits.
    /// Every intermediate cell must be visited or empty (<see cref="IsPassableIntermediateTile"/>).
    /// Returns false when no such path exists.
    /// </summary>
    public static bool TryBuildMovePath(MapGrid grid, Vector2Int from, Vector2Int to, List<Vector2Int> path)
    {
        path?.Clear();
        if (path == null || grid == null || !grid.Contains(from) || !grid.Contains(to) || from == to)
            return false;

        var previous = new Dictionary<Vector2Int, Vector2Int>();
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);

        while (q.Count > 0)
        {
            var current = q.Dequeue();
            if (current == to)
            {
                ReconstructPath(previous, from, to, path);
                return path.Count >= 2;
            }

            var tile = grid.Get(current.x, current.y);
            for (var di = 0; di < 4; di++)
            {
                var dir = (MapCardinalDirection)di;
                if (!tile.exitMask.Contains(dir))
                    continue;

                var next = current + dir.ToDelta();
                if (!grid.Contains(next) || previous.ContainsKey(next))
                    continue;

                var nextTile = grid.Get(next.x, next.y);
                if (next != to && !IsTraversableIntermediateTile(nextTile))
                    continue;

                previous[next] = current;
                q.Enqueue(next);
            }
        }

        path.Clear();
        return false;
    }

    static void ReconstructPath(Dictionary<Vector2Int, Vector2Int> previous, Vector2Int from, Vector2Int to, List<Vector2Int> path)
    {
        path.Clear();
        var cursor = to;
        while (true)
        {
            path.Add(cursor);
            if (cursor == from)
                break;
            cursor = previous[cursor];
        }

        path.Reverse();
    }
}
