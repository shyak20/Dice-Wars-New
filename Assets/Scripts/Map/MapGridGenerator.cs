using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapConnectivityMode
{
    /// <summary>Random exits, bidirectional fix, BFS until start→boss reachable (multiple routes possible).</summary>
    MultiPathRandom,
    /// <summary>Random spanning tree of the full grid: exactly one simple path between any two tiles (and start→boss).</summary>
    UniquePathSpanningTree
}

/// <summary>Map generation: multi-path random or unique-path spanning tree.</summary>
public static class MapGridGenerator
{
    private static readonly MapNodeType[] NodeTypes =
    {
        MapNodeType.Combat,
        MapNodeType.Shop,
        MapNodeType.Treasure,
        MapNodeType.Mystery
    };

    public const int DefaultMaxRegenerateAttempts = 5000;

    /// <summary>Start = (0,0), end = (width-1, height-1).</summary>
    public static MapGrid Generate(
        int width,
        int height,
        System.Random rng,
        MapConnectivityMode mode = MapConnectivityMode.MultiPathRandom,
        int maxAttempts = DefaultMaxRegenerateAttempts)
    {
        if (rng == null) throw new ArgumentNullException(nameof(rng));
        if (width < 2 || height < 2)
            throw new ArgumentOutOfRangeException(nameof(width), "Map must be at least 2×2 for start and boss.");

        var start = new Vector2Int(0, 0);
        var end = new Vector2Int(width - 1, height - 1);

        if (mode == MapConnectivityMode.UniquePathSpanningTree)
        {
            var treeGrid = GenerateUniquePathSpanningTree(width, height, rng);
            EnsureNoIsolatedNonEndTiles(treeGrid, end, rng);
            EnsureEveryTileReachableFromStart(treeGrid, start, rng);
            AssignRandomNodeTypes(treeGrid, rng, start, end);
            StripEndTileExits(treeGrid, end);
            return treeGrid;
        }

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var grid = new MapGrid(width, height);
            FillRandomExits(grid, rng);
            EnsureAtLeastOneExitPreferIncoming(grid, rng);
            EnsureBidirectionalExits(grid);
            EnsureNoIsolatedNonEndTiles(grid, end, rng);
            EnsureEveryTileReachableFromStart(grid, start, rng);
            AssignRandomNodeTypes(grid, rng, start, end);
            if (!MapPathfinding.HasPath(grid, start, end))
                continue;
            StripEndTileExits(grid, end);
            return grid;
        }

        throw new InvalidOperationException(
            $"MapGridGenerator: could not build a connected map after {maxAttempts} attempts. Check RNG or constraints.");
    }

    /// <summary>Randomized Kruskal spanning tree on the 4-neighbor grid; all links are bidirectional.</summary>
    private static MapGrid GenerateUniquePathSpanningTree(int width, int height, System.Random rng)
    {
        var grid = new MapGrid(width, height);
        var edges = new List<(int x, int y, MapCardinalDirection d)>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (x + 1 < width)
                    edges.Add((x, y, MapCardinalDirection.Right));
                if (y + 1 < height)
                    edges.Add((x, y, MapCardinalDirection.Bottom));
            }
        }

        Shuffle(edges, rng);

        var n = width * height;
        var parent = new int[n];
        for (var i = 0; i < n; i++)
            parent[i] = i;

        foreach (var (x, y, d) in edges)
        {
            var a = y * width + x;
            var neighbor = new Vector2Int(x, y) + d.ToDelta();
            var b = neighbor.y * width + neighbor.x;
            if (Find(parent, a) == Find(parent, b))
                continue;
            Union(parent, a, b);
            AddBidirectionalEdge(grid, x, y, d);
        }

        return grid;
    }

    private static void AddBidirectionalEdge(MapGrid grid, int x, int y, MapCardinalDirection d)
    {
        var n = new Vector2Int(x, y) + d.ToDelta();
        var back = d.Opposite();

        var tA = grid.Get(x, y);
        tA.exitMask = tA.exitMask.With(d);
        grid.SetTile(x, y, tA);

        var tB = grid.Get(n.x, n.y);
        tB.exitMask = tB.exitMask.With(back);
        grid.SetTile(n.x, n.y, tB);
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }

        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra != rb)
            parent[rb] = ra;
    }

    private static void FillRandomExits(MapGrid grid, System.Random rng)
    {
        var dirsScratch = new List<MapCardinalDirection>(4);
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                dirsScratch.Clear();
                for (var di = 0; di < 4; di++)
                {
                    var d = (MapCardinalDirection)di;
                    var n = new Vector2Int(x, y) + d.ToDelta();
                    if (grid.Contains(n))
                        dirsScratch.Add(d);
                }

                var available = dirsScratch.Count;
                if (available == 0)
                    continue;

                var maxExits = Mathf.Min(3, available);
                var exitCount = rng.Next(0, maxExits + 1);

                Shuffle(dirsScratch, rng);
                var mask = 0;
                for (var i = 0; i < exitCount; i++)
                    mask = mask.With(dirsScratch[i]);

                grid.SetExitMask(x, y, mask);
            }
        }
    }

    /// <summary>
    /// Tiles with no exits get one exit toward a neighbor that already has an edge into this tile (reachable-from).
    /// If none (e.g. start with no inbound edges yet), picks a random in-bounds direction.
    /// </summary>
    private static void EnsureAtLeastOneExitPreferIncoming(MapGrid grid, System.Random rng)
    {
        var incomingDirs = new List<MapCardinalDirection>(4);
        var fallbackDirs = new List<MapCardinalDirection>(4);

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                if (grid.Get(x, y).exitMask != 0)
                    continue;

                incomingDirs.Clear();
                fallbackDirs.Clear();

                for (var di = 0; di < 4; di++)
                {
                    var d = (MapCardinalDirection)di;
                    var n = new Vector2Int(x, y) + d.ToDelta();
                    if (!grid.Contains(n))
                        continue;
                    fallbackDirs.Add(d);
                    if (grid.HasExit(n.x, n.y, d.Opposite()))
                        incomingDirs.Add(d);
                }

                if (fallbackDirs.Count == 0)
                    continue;

                var pick = incomingDirs.Count > 0
                    ? incomingDirs[rng.Next(incomingDirs.Count)]
                    : fallbackDirs[rng.Next(fallbackDirs.Count)];

                var t = grid.Get(x, y);
                t.exitMask = t.exitMask.With(pick);
                grid.SetTile(x, y, t);
            }
        }
    }

    /// <summary>For every exit A→B, ensures B→A so no one-way dead ends.</summary>
    private static void EnsureBidirectionalExits(MapGrid grid)
    {
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                for (var di = 0; di < 4; di++)
                {
                    var d = (MapCardinalDirection)di;
                    if (!grid.HasExit(x, y, d))
                        continue;
                    var n = new Vector2Int(x, y) + d.ToDelta();
                    if (!grid.Contains(n))
                        continue;
                    var back = d.Opposite();
                    if (grid.HasExit(n.x, n.y, back))
                        continue;
                    var t = grid.Get(n.x, n.y);
                    t.exitMask = t.exitMask.With(back);
                    grid.SetTile(n.x, n.y, t);
                }
            }
        }
    }

    /// <summary>
    /// Every cell except the end tile must have at least one passage (outgoing or incoming from a neighbor).
    /// The end tile is handled separately by <see cref="StripEndTileExits"/> (sink with incoming links only).
    /// </summary>
    private static void EnsureNoIsolatedNonEndTiles(MapGrid grid, Vector2Int endCell, System.Random rng)
    {
        var neighborDirs = new List<MapCardinalDirection>(4);
        var maxPasses = grid.Width * grid.Height + 4;

        for (var pass = 0; pass < maxPasses; pass++)
        {
            var fixedAny = false;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (x == endCell.x && y == endCell.y)
                        continue;
                    if (CellHasAnyGridConnection(grid, x, y))
                        continue;

                    fixedAny = true;
                    neighborDirs.Clear();
                    var p = new Vector2Int(x, y);
                    for (var di = 0; di < 4; di++)
                    {
                        var d = (MapCardinalDirection)di;
                        if (grid.Contains(p + d.ToDelta()))
                            neighborDirs.Add(d);
                    }

                    if (neighborDirs.Count == 0)
                        continue;

                    Shuffle(neighborDirs, rng);
                    AddBidirectionalEdge(grid, x, y, neighborDirs[0]);
                }
            }

            if (!fixedAny)
                break;
        }
    }

    /// <summary>
    /// Bridges any tile not reachable from <paramref name="start"/> by adding bidirectional edges to an orthogonally adjacent reachable tile.
    /// Run before <see cref="StripEndTileExits"/> so the boss cell can still be entered via directed BFS.
    /// </summary>
    private static void EnsureEveryTileReachableFromStart(MapGrid grid, Vector2Int start, System.Random rng)
    {
        var total = grid.Width * grid.Height;
        var bridgeDirs = new List<MapCardinalDirection>(4);

        for (var iter = 0; iter < total + 8; iter++)
        {
            var reachable = MapPathfinding.ReachableFromStart(grid, start);
            if (reachable.Count >= total)
                return;

            var bridged = false;
            for (var y = 0; y < grid.Height && !bridged; y++)
            {
                for (var x = 0; x < grid.Width && !bridged; x++)
                {
                    var p = new Vector2Int(x, y);
                    if (reachable.Contains(p))
                        continue;

                    bridgeDirs.Clear();
                    for (var di = 0; di < 4; di++)
                    {
                        var d = (MapCardinalDirection)di;
                        var n = p + d.ToDelta();
                        if (grid.Contains(n) && reachable.Contains(n))
                            bridgeDirs.Add(d);
                    }

                    if (bridgeDirs.Count == 0)
                        continue;

                    Shuffle(bridgeDirs, rng);
                    AddBidirectionalEdge(grid, p.x, p.y, bridgeDirs[0]);
                    bridged = true;
                }
            }

            if (!bridged)
            {
                Debug.LogError("MapGridGenerator: unreachable tiles could not be linked to the start component.");
                return;
            }
        }
    }

    private static bool CellHasAnyGridConnection(MapGrid grid, int x, int y)
    {
        if (grid.Get(x, y).exitMask != 0)
            return true;

        var p = new Vector2Int(x, y);
        for (var di = 0; di < 4; di++)
        {
            var d = (MapCardinalDirection)di;
            var n = p + d.ToDelta();
            if (!grid.Contains(n))
                continue;
            if (grid.HasExit(n.x, n.y, d.Opposite()))
                return true;
        }

        return false;
    }

    /// <summary>Boss / end cell is a sink: no outgoing exits (neighbors may still lead into it).</summary>
    private static void StripEndTileExits(MapGrid grid, Vector2Int endCell)
    {
        if (!grid.Contains(endCell))
            return;
        var t = grid.Get(endCell.x, endCell.y);
        t.exitMask = 0;
        grid.SetTile(endCell.x, endCell.y, t);
    }

    private static void AssignRandomNodeTypes(MapGrid grid, System.Random rng, Vector2Int start, Vector2Int end)
    {
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (p == start || p == end)
                    continue;
                var t = grid.Get(x, y);
                t.nodeType = NodeTypes[rng.Next(NodeTypes.Length)];
                grid.SetTile(x, y, t);
            }
        }
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
