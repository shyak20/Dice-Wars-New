using UnityEngine;

/// <summary>Resolves rank portraits from <see cref="PlayerRankSO"/> for a character's active progression rank.</summary>
public static class ProgressionRankPortraitUtility
{
    public static Sprite GetPortrait(PlayerDataSO character, bool useSmallPortrait = false)
    {
        var rank = GetActiveRank(character);
        if (rank == null)
            return null;

        return useSmallPortrait ? rank.SmallPortrait : rank.Portrait;
    }

    /// <summary>
    /// Active rank for <paramref name="character"/> from that character's saved
    /// <see cref="ProgressionProfileSaveData.currentRankIndex"/> (each roster entry has its own save).
    /// </summary>
    public static PlayerRankSO GetActiveRank(PlayerDataSO character)
    {
        if (character?.progressionCatalog == null)
            return null;

        var save = ProgressionSaveService.Load(character.MetaSaveId);
        var rankIndex = save != null ? save.currentRankIndex : 0;
        return character.progressionCatalog.GetRankOrNull(rankIndex);
    }

    public static bool TryGetNextRank(PlayerDataSO character, PlayerRankSO currentRank, out PlayerRankSO nextRank)
    {
        nextRank = null;
        if (character?.progressionCatalog == null || currentRank == null)
            return false;

        return character.progressionCatalog.TryGetNextRank(currentRank.rankIndex, out nextRank);
    }
}
