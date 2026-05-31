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
    /// Active rank for <paramref name="character"/> from progression runtime when bound,
    /// otherwise from that character's saved <see cref="ProgressionProfileSaveData.currentRankIndex"/>.
    /// </summary>
    public static PlayerRankSO GetActiveRank(PlayerDataSO character)
    {
        if (character == null || character.progressionCatalog == null)
            return null;

        var catalog = character.progressionCatalog;
        var progression = ProgressionManager.TryGetRuntime();

        if (progression != null && progression.IsInitializedFor(character))
            return progression.GetActiveRank();

        var save = ProgressionSaveService.Load(character.MetaSaveId);
        var rankIndex = save != null ? save.currentRankIndex : 0;
        return catalog.GetRankOrNull(rankIndex);
    }

    public static bool TryGetNextRank(PlayerDataSO character, PlayerRankSO currentRank, out PlayerRankSO nextRank)
    {
        nextRank = null;
        if (character?.progressionCatalog == null || currentRank == null)
            return false;

        return character.progressionCatalog.TryGetNextRank(currentRank.rankIndex, out nextRank);
    }
}
