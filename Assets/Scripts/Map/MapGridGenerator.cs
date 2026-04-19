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
    public const int DefaultMaxRegenerateAttempts = 5000;

    /// <summary>Start = (0,0), end = (width-1, height-1).</summary>
    public static MapGrid Generate(
        int width,
        int height,
        System.Random rng,
        MapConnectivityMode mode = MapConnectivityMode.MultiPathRandom,
        int maxAttempts = DefaultMaxRegenerateAttempts)
    {
        return Generate(width, height, rng, mode, maxAttempts, MapGenerationEventsParams.Default);
    }

    /// <summary>Start = (0,0), end = (width-1, height-1). Assigns <see cref="MapEventType"/> per <paramref name="mapEvents"/>.</summary>
    public static MapGrid Generate(
        int width,
        int height,
        System.Random rng,
        MapConnectivityMode mode,
        int maxAttempts,
        MapGenerationEventsParams mapEvents)
    {
        if (rng == null) throw new ArgumentNullException(nameof(rng));
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Map width and height must be at least 1.");

        var start = new Vector2Int(0, 0);
        var end = new Vector2Int(width - 1, height - 1);

        if (mode == MapConnectivityMode.UniquePathSpanningTree)
        {
            var treeGrid = GenerateUniquePathSpanningTree(width, height, rng);
            EnsureNoIsolatedNonEndTiles(treeGrid, end, rng);
            EnsureEveryTileReachableFromStart(treeGrid, start, rng);
            AssignMapEventTypes(treeGrid, rng, start, end, mapEvents);
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
            AssignMapEventTypes(grid, rng, start, end, mapEvents);
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

    /// <summary>Start stays <see cref="MapEventType.None"/>; end is <see cref="MapEventType.CombatBoss"/>; elites are not orthogonally adjacent.</summary>
    private static void AssignMapEventTypes(MapGrid grid, System.Random rng, Vector2Int start, Vector2Int end, MapGenerationEventsParams p)
    {
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var t = grid.Get(x, y);
                t.eventType = MapEventType.None;
                grid.SetTile(x, y, t);
            }
        }

        var tEnd = grid.Get(end.x, end.y);
        tEnd.eventType = MapEventType.CombatBoss;
        grid.SetTile(end.x, end.y, tEnd);

        var distFromStart = MapPathfinding.ComputeDirectedStepsFromStart(grid, start);

        var candidates = new List<Vector2Int>();
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var pos = new Vector2Int(x, y);
                if (pos == start || pos == end)
                    continue;
                candidates.Add(pos);
            }
        }

        Shuffle(candidates, rng);

        var eliteMin = Mathf.Max(0, p.EliteMinCount);
        var eliteMax = Mathf.Max(eliteMin, p.EliteMaxCount);
        var eliteTarget = rng.Next(eliteMin, eliteMax + 1);
        eliteTarget = Mathf.Min(eliteTarget, candidates.Count);

        var eliteCells = new HashSet<Vector2Int>();
        foreach (var c in candidates)
        {
            if (eliteCells.Count >= eliteTarget)
                break;
            if (p.MinStepsElite > 0)
            {
                var steps = MapPathfinding.GetDirectedSteps(distFromStart, grid.Width, c);
                if (steps < p.MinStepsElite)
                    continue;
            }

            if (HasOrthogonalNeighborInSet(c, eliteCells))
                continue;
            eliteCells.Add(c);
        }

        if (eliteCells.Count < eliteMin)
            Debug.LogWarning(
                $"MapGridGenerator: placed {eliteCells.Count} elite tile(s) but elite min was {eliteMin} (grid {grid.Width}×{grid.Height}).");

        foreach (var e in eliteCells)
        {
            var t = grid.Get(e.x, e.y);
            t.eventType = MapEventType.CombatElite;
            grid.SetTile(e.x, e.y, t);
        }

        var filler = new List<Vector2Int>();
        foreach (var c in candidates)
        {
            if (!eliteCells.Contains(c))
                filler.Add(c);
        }

        Shuffle(filler, rng);

        if (p.UseLegacyWeightedFillers)
        {
            var shopMin = Mathf.Max(0, p.ShopMinCount);
            var shopMax = Mathf.Max(shopMin, p.ShopMaxCount);
            var useFixedShopCount = shopMin > 0 || shopMax > 0;
            var shopTarget = useFixedShopCount ? rng.Next(shopMin, shopMax + 1) : 0;
            var shopPool = new List<Vector2Int>();
            foreach (var c in filler)
            {
                if (p.MinStepsShop > 0)
                {
                    var st = MapPathfinding.GetDirectedSteps(distFromStart, grid.Width, c);
                    if (st < p.MinStepsShop)
                        continue;
                }

                shopPool.Add(c);
            }

            Shuffle(shopPool, rng);
            shopTarget = Mathf.Min(shopTarget, shopPool.Count);
            if (useFixedShopCount && shopTarget < shopMin)
                Debug.LogWarning(
                    $"MapGridGenerator: placed {shopTarget} shop tile(s) but shop min was {shopMin} (grid {grid.Width}×{grid.Height}).");

            var shopCells = new HashSet<Vector2Int>();
            for (var i = 0; i < shopTarget; i++)
                shopCells.Add(shopPool[i]);

            foreach (var c in candidates)
            {
                if (eliteCells.Contains(c))
                    continue;
                var t = grid.Get(c.x, c.y);
                if (shopCells.Contains(c))
                    t.eventType = MapEventType.Shop;
                else if (useFixedShopCount)
                    t.eventType = PickNonShopFillerTypeRespectingMinSteps(distFromStart, grid.Width, c, p, rng);
                else
                    t.eventType = PickNonEliteEventTypeRespectingMinSteps(distFromStart, grid.Width, c, p, rng);
                grid.SetTile(c.x, c.y, t);
            }
        }
        else
        {
            var specialCounts = SampleSpecialFillerTypeCounts(filler.Count, p, rng);
            AssignFillerWithNormalFallback(grid, filler, specialCounts, distFromStart, p, rng);
        }
    }

    private static readonly MapEventType[] SpecialFillerTypes =
    {
        MapEventType.Shop,
        MapEventType.Shrine,
        MapEventType.Unknown,
        MapEventType.Treasure
    };

    /// <summary>Assigns Shop/Shrine/Unknown/Treasure counts, then fills any remaining shuffled cells with <see cref="MapEventType.CombatNormal"/>.</summary>
    private static void AssignFillerWithNormalFallback(
        MapGrid grid,
        List<Vector2Int> fillerShuffled,
        int[] specialCounts,
        int[] distFromStart,
        MapGenerationEventsParams p,
        System.Random rng)
    {
        var width = grid.Width;
        var available = new List<Vector2Int>(fillerShuffled);
        var minStepsBySpecialIndex = new[]
        {
            p.MinStepsShop,
            p.MinStepsShrine,
            p.MinStepsUnknown,
            p.MinStepsTreasure
        };

        for (var t = 0; t < SpecialFillerTypes.Length; t++)
        {
            var minS = minStepsBySpecialIndex[t];
            for (var n = 0; n < specialCounts[t]; n++)
            {
                if (!TryPickFillerCell(available, distFromStart, width, minS, rng, SpecialFillerTypes[t], out var pos))
                {
                    Debug.LogError(
                        $"MapGridGenerator: could not place {SpecialFillerTypes[t]} (min steps {minS}, remaining pool {available.Count}).");
                    return;
                }

                var tile = grid.Get(pos.x, pos.y);
                tile.eventType = SpecialFillerTypes[t];
                grid.SetTile(pos.x, pos.y, tile);
            }
        }

        foreach (var pos in available)
        {
            var tile = grid.Get(pos.x, pos.y);
            tile.eventType = MapEventType.CombatNormal;
            grid.SetTile(pos.x, pos.y, tile);
        }
    }

    /// <summary>Picks and removes one cell from <paramref name="pool"/> honoring <paramref name="minSteps"/> when &gt; 0.</summary>
    private static bool TryPickFillerCell(
        List<Vector2Int> pool,
        int[] distFromStart,
        int width,
        int minSteps,
        System.Random rng,
        MapEventType forLog,
        out Vector2Int pos)
    {
        pos = default;
        if (pool.Count == 0)
            return false;

        if (minSteps <= 0)
        {
            var i = rng.Next(pool.Count);
            pos = pool[i];
            pool.RemoveAt(i);
            return true;
        }

        var eligibleIdx = new List<int>();
        for (var i = 0; i < pool.Count; i++)
        {
            var d = MapPathfinding.GetDirectedSteps(distFromStart, width, pool[i]);
            if (d >= minSteps)
                eligibleIdx.Add(i);
        }

        if (eligibleIdx.Count > 0)
        {
            var pick = eligibleIdx[rng.Next(eligibleIdx.Count)];
            pos = pool[pick];
            pool.RemoveAt(pick);
            return true;
        }

        Debug.LogWarning(
            $"MapGridGenerator: MinStepsFromStart for {forLog} not satisfiable with {pool.Count} tile(s); placing without distance.");
        var j = rng.Next(pool.Count);
        pos = pool[j];
        pool.RemoveAt(j);
        return true;
    }

    /// <summary>
    /// Rolls a count for each special type independently in [min, max]. Min=max=0 disables that type.
    /// If the sum exceeds available filler cells, reduces random counts (above mins) until it fits.
    /// Any filler cells left unassigned become <see cref="MapEventType.CombatNormal"/>.
    /// </summary>
    private static int[] SampleSpecialFillerTypeCounts(int fillerTotal, MapGenerationEventsParams p, System.Random rng)
    {
        var mins = new[]
        {
            Mathf.Max(0, p.ShopMinCount),
            Mathf.Max(0, p.ShrineMinCount),
            Mathf.Max(0, p.UnknownMinCount),
            Mathf.Max(0, p.TreasureMinCount)
        };
        var maxsRaw = new[]
        {
            Mathf.Max(0, p.ShopMaxCount),
            Mathf.Max(0, p.ShrineMaxCount),
            Mathf.Max(0, p.UnknownMaxCount),
            Mathf.Max(0, p.TreasureMaxCount)
        };

        var counts = new int[4];
        for (var i = 0; i < 4; i++)
        {
            // Both 0 => this event type is off; remaining tiles use normal combat fallback only.
            if (mins[i] == 0 && maxsRaw[i] == 0)
            {
                counts[i] = 0;
                continue;
            }

            var hi = maxsRaw[i] == 0 ? fillerTotal : Mathf.Min(maxsRaw[i], fillerTotal);
            hi = Mathf.Max(hi, mins[i]);
            hi = Mathf.Min(hi, fillerTotal);
            if (mins[i] > hi)
            {
                Debug.LogWarning(
                    $"MapGridGenerator: filler min exceeds available cells for special index {i} (min {mins[i]}, cap {fillerTotal}).");
                counts[i] = Mathf.Min(mins[i], fillerTotal);
            }
            else
                counts[i] = rng.Next(mins[i], hi + 1);
        }

        var sum = counts[0] + counts[1] + counts[2] + counts[3];
        while (sum > fillerTotal)
        {
            var reducible = new List<int>();
            for (var i = 0; i < 4; i++)
            {
                if (counts[i] > mins[i])
                    reducible.Add(i);
            }

            if (reducible.Count == 0)
            {
                Debug.LogWarning(
                    $"MapGridGenerator: special counts sum to {sum} but only {fillerTotal} filler tiles — trimming.");
                for (var i = 3; i >= 0 && sum > fillerTotal; i--)
                {
                    while (counts[i] > 0 && sum > fillerTotal)
                    {
                        counts[i]--;
                        sum--;
                    }
                }

                break;
            }

            var pick = reducible[rng.Next(reducible.Count)];
            counts[pick]--;
            sum--;
        }

        return counts;
    }

    private static bool HasOrthogonalNeighborInSet(Vector2Int c, HashSet<Vector2Int> set)
    {
        return set.Contains(c + new Vector2Int(1, 0))
               || set.Contains(c + new Vector2Int(-1, 0))
               || set.Contains(c + new Vector2Int(0, 1))
               || set.Contains(c + new Vector2Int(0, -1));
    }

    private static int MinStepsForMapEvent(MapEventType evt, MapGenerationEventsParams p)
    {
        return evt switch
        {
            MapEventType.Shop => p.MinStepsShop,
            MapEventType.Shrine => p.MinStepsShrine,
            MapEventType.Unknown => p.MinStepsUnknown,
            MapEventType.Treasure => p.MinStepsTreasure,
            _ => 0
        };
    }

    private static bool CellMeetsMinStepsForType(int[] distFromStart, int width, Vector2Int cell, MapEventType evt, MapGenerationEventsParams p)
    {
        var min = MinStepsForMapEvent(evt, p);
        if (min <= 0) return true;
        var steps = MapPathfinding.GetDirectedSteps(distFromStart, width, cell);
        return steps >= min;
    }

    private static MapEventType PickNonShopFillerTypeRespectingMinSteps(
        int[] distFromStart,
        int width,
        Vector2Int cell,
        MapGenerationEventsParams p,
        System.Random rng)
    {
        for (var attempt = 0; attempt < 48; attempt++)
        {
            var pick = PickNonShopFillerType(p, rng);
            if (CellMeetsMinStepsForType(distFromStart, width, cell, pick, p))
                return pick;
        }

        return MapEventType.CombatNormal;
    }

    private static MapEventType PickNonEliteEventTypeRespectingMinSteps(
        int[] distFromStart,
        int width,
        Vector2Int cell,
        MapGenerationEventsParams p,
        System.Random rng)
    {
        for (var attempt = 0; attempt < 48; attempt++)
        {
            var pick = PickNonEliteEventType(p, rng);
            if (CellMeetsMinStepsForType(distFromStart, width, cell, pick, p))
                return pick;
        }

        return MapEventType.CombatNormal;
    }

    private static MapEventType PickNonEliteEventType(MapGenerationEventsParams p, System.Random rng)
    {
        var wN = Mathf.Max(0, p.WeightNormal);
        var wShop = Mathf.Max(0, p.WeightShop);
        var wShrine = Mathf.Max(0, p.WeightShrine);
        var wUnk = Mathf.Max(0, p.WeightUnknown);
        var wTreasure = Mathf.Max(0, p.WeightTreasure);
        var total = wN + wShop + wShrine + wUnk + wTreasure;
        if (total <= 0)
            return MapEventType.CombatNormal;

        var r = rng.Next(total);
        if (r < wN)
            return MapEventType.CombatNormal;
        r -= wN;
        if (r < wShop)
            return MapEventType.Shop;
        r -= wShop;
        if (r < wShrine)
            return MapEventType.Shrine;
        r -= wShrine;
        if (r < wUnk)
            return MapEventType.Unknown;
        return MapEventType.Treasure;
    }

    /// <summary>Remaining fillers after fixed shop tiles: normal / shrine / unknown only.</summary>
    private static MapEventType PickNonShopFillerType(MapGenerationEventsParams p, System.Random rng)
    {
        var wN = Mathf.Max(0, p.WeightNormal);
        var wShrine = Mathf.Max(0, p.WeightShrine);
        var wUnk = Mathf.Max(0, p.WeightUnknown);
        var wTreasure = Mathf.Max(0, p.WeightTreasure);
        var total = wN + wShrine + wUnk + wTreasure;
        if (total <= 0)
            return MapEventType.CombatNormal;

        var r = rng.Next(total);
        if (r < wN)
            return MapEventType.CombatNormal;
        r -= wN;
        if (r < wShrine)
            return MapEventType.Shrine;
        r -= wShrine;
        if (r < wUnk)
            return MapEventType.Unknown;
        return MapEventType.Treasure;
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
