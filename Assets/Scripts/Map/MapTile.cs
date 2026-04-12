/// <summary>One cell on the run map: directed exits + node type.</summary>
public struct MapTile
{
    public MapNodeType nodeType;
    /// <summary>Directed edges: bit per <see cref="MapCardinalDirection"/>.</summary>
    public int exitMask;
}
