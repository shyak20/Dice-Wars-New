using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Closes one random passage (both directions) only if the player can still reach the end tile from their current cell.
/// </summary>
public static class MapCorruptionUtility
{
    public const int DefaultMaxAttempts = 400;

    public static bool TryCloseOneRandomExitPreservingPath(
        MapGrid grid,
        Vector2Int playerPosition,
        Vector2Int end,
        System.Random rng,
        int maxAttempts = DefaultMaxAttempts)
    {
        if (grid == null || rng == null) return false;
        if (!grid.Contains(playerPosition) || !grid.Contains(end))
            return false;

        var candidates = new List<(int x, int y, MapCardinalDirection d)>();
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var mask = grid.Get(x, y).exitMask;
                for (var di = 0; di < 4; di++)
                {
                    var d = (MapCardinalDirection)di;
                    if (mask.Contains(d))
                        candidates.Add((x, y, d));
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var idx = rng.Next(candidates.Count);
            var (x, y, d) = candidates[idx];
            if (!grid.HasExit(x, y, d))
                continue;

            var n = new Vector2Int(x, y) + d.ToDelta();
            if (!grid.Contains(n))
                continue;

            var back = d.Opposite();
            grid.RemoveExit(x, y, d);
            grid.RemoveExit(n.x, n.y, back);

            if (MapPathfinding.HasPath(grid, playerPosition, end))
                return true;

            var tA = grid.Get(x, y);
            tA.exitMask = tA.exitMask.With(d);
            grid.SetTile(x, y, tA);
            var tB = grid.Get(n.x, n.y);
            tB.exitMask = tB.exitMask.With(back);
            grid.SetTile(n.x, n.y, tB);
        }

        return false;
    }
}
