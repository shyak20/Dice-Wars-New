using System;

/// <summary>Parameters for assigning <see cref="MapEventType"/> after connectivity is built.</summary>
[Serializable]
public struct MapGenerationEventsParams
{
    public int EliteMinCount;
    public int EliteMaxCount;

    public int ShopMinCount;
    public int ShopMaxCount;

    public int ShrineMinCount;
    public int ShrineMaxCount;

    public int UnknownMinCount;
    public int UnknownMaxCount;

    public int TreasureMinCount;
    public int TreasureMaxCount;

    /// <summary>Minimum directed path length from map start (steps along exits). 0 = no constraint.</summary>
    public int MinStepsElite;
    public int MinStepsShop;
    public int MinStepsShrine;
    public int MinStepsUnknown;
    public int MinStepsTreasure;

    public int WeightNormal;
    public int WeightShop;
    public int WeightShrine;
    public int WeightUnknown;
    public int WeightTreasure;

    /// <summary>True when Shrine/Unknown/Treasure min+max are all zero — generator uses RunManager weights for fillers (legacy).</summary>
    public bool UseLegacyWeightedFillers =>
        ShrineMinCount == 0 && ShrineMaxCount == 0
        && UnknownMinCount == 0 && UnknownMaxCount == 0
        && TreasureMinCount == 0 && TreasureMaxCount == 0;

    public static MapGenerationEventsParams Default => new MapGenerationEventsParams
    {
        EliteMinCount = 1,
        EliteMaxCount = 2,
        ShopMinCount = 0,
        ShopMaxCount = 0,
        ShrineMinCount = 0,
        ShrineMaxCount = 0,
        UnknownMinCount = 0,
        UnknownMaxCount = 0,
        TreasureMinCount = 0,
        TreasureMaxCount = 0,
        WeightNormal = 4,
        WeightShop = 2,
        WeightShrine = 2,
        WeightUnknown = 1,
        WeightTreasure = 1,
        MinStepsElite = 0,
        MinStepsShop = 0,
        MinStepsShrine = 0,
        MinStepsUnknown = 0,
        MinStepsTreasure = 0
    };
}
