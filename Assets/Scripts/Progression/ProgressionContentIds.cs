using UnityEngine;

/// <summary>Stable string keys for horizontal unlocks (ScriptableObject asset names by default).</summary>
public static class ProgressionContentIds
{
    public static string For(Object asset)
    {
        if (asset == null)
            return string.Empty;
        return asset.name;
    }

    public static string ForDie(DieAssetSO die) => For(die);
    public static string ForFace(DieFaceSO face) => For(face);
    public static string ForGem(GemSO gem) => For(gem);
    public static string ForRelic(RelicSO relic) => For(relic);

    public static bool IsNullOrEmpty(string id) => string.IsNullOrWhiteSpace(id);
}
