using System;
using UnityEngine;

/// <summary>Width × height map with per-tile directed exits and node types.</summary>
public sealed class MapGrid
{
    private readonly MapTile[] _tiles;

    public int Width { get; }
    public int Height { get; }

    public MapGrid(int width, int height)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be at least 1.");
        Width = width;
        Height = height;
        _tiles = new MapTile[width * height];
    }

    private int Index(int x, int y) => y * Width + x;

    public bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public bool Contains(Vector2Int p) => Contains(p.x, p.y);

    public MapTile Get(int x, int y) => _tiles[Index(x, y)];

    public MapTile Get(Vector2Int p) => Get(p.x, p.y);

    public void SetTile(int x, int y, MapTile tile) => _tiles[Index(x, y)] = tile;

    public void SetExitMask(int x, int y, int exitMask)
    {
        var i = Index(x, y);
        var t = _tiles[i];
        t.exitMask = exitMask;
        _tiles[i] = t;
    }

    public void SetNodeType(int x, int y, MapNodeType type)
    {
        var i = Index(x, y);
        var t = _tiles[i];
        t.nodeType = type;
        _tiles[i] = t;
    }

    public bool HasExit(int x, int y, MapCardinalDirection d)
    {
        if (!Contains(x, y)) return false;
        return Get(x, y).exitMask.Contains(d);
    }

    public void RemoveExit(int x, int y, MapCardinalDirection d)
    {
        if (!Contains(x, y)) return;
        var i = Index(x, y);
        var t = _tiles[i];
        t.exitMask = t.exitMask.Without(d);
        _tiles[i] = t;
    }

    public MapGrid Clone()
    {
        var c = new MapGrid(Width, Height);
        Array.Copy(_tiles, c._tiles, _tiles.Length);
        return c;
    }
}
