using System;

/// <summary>Parameters for assigning <see cref="MapEventType"/> after connectivity is built.</summary>
[Serializable]
public struct MapGenerationEventsParams
{
    public int EliteMinCount;
    public int EliteMaxCount;
    /// <summary>When both are 0, shop count comes only from <see cref="WeightShop"/> among non-elite fillers.</summary>
    public int ShopMinCount;
    public int ShopMaxCount;
    public int WeightNormal;
    public int WeightShop;
    public int WeightShrine;
    public int WeightUnknown;

    public static MapGenerationEventsParams Default => new MapGenerationEventsParams
    {
        EliteMinCount = 1,
        EliteMaxCount = 2,
        ShopMinCount = 0,
        ShopMaxCount = 0,
        WeightNormal = 4,
        WeightShop = 2,
        WeightShrine = 2,
        WeightUnknown = 1
    };
}
