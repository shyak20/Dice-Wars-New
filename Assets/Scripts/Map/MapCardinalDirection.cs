using UnityEngine;

/// <summary>Grid exits: Top = smaller Y (row up on screen if row 0 is top).</summary>
public enum MapCardinalDirection
{
    Top = 0,
    Right = 1,
    Bottom = 2,
    Left = 3
}

public static class MapCardinalDirectionExtensions
{
    private static readonly Vector2Int[] Deltas =
    {
        new Vector2Int(0, -1), // Top
        new Vector2Int(1, 0),  // Right
        new Vector2Int(0, 1),  // Bottom
        new Vector2Int(-1, 0)  // Left
    };

    public static Vector2Int ToDelta(this MapCardinalDirection d) => Deltas[(int)d];

    public static MapCardinalDirection Opposite(this MapCardinalDirection d)
    {
        return (MapCardinalDirection)(((int)d + 2) % 4);
    }

    public static int Bit(this MapCardinalDirection d) => 1 << (int)d;

    public static bool Contains(this int exitMask, MapCardinalDirection d) => (exitMask & d.Bit()) != 0;

    public static int With(this int exitMask, MapCardinalDirection d) => exitMask | d.Bit();

    public static int Without(this int exitMask, MapCardinalDirection d) => exitMask & ~d.Bit();
}
