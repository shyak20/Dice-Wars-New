/// <summary>One cell on the run map: directed exits + event type.</summary>
public struct MapTile
{
    public MapEventType eventType;
    /// <summary>Directed edges: bit per <see cref="MapCardinalDirection"/>.</summary>
    public int exitMask;
    /// <summary>Player has landed here once; event will not run again and UI shows visited marker.</summary>
    public bool eventConsumed;
}
