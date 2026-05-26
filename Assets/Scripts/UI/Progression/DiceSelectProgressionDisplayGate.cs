using System;

/// <summary>
/// While <see cref="IsDeferred"/> is true, Dice Select progression displays (deck, trials, stats)
/// should not refresh; call <see cref="SetDeferred"/> with false after celebration popups finish.
/// </summary>
public static class DiceSelectProgressionDisplayGate
{
    public static bool IsDeferred { get; private set; }

    public static event Action DeferredRefreshRequested;

    public static bool ShouldRefreshProgressionDisplays() => !IsDeferred;

    public static bool HasPendingCelebrationsFor(PlayerDataSO character)
    {
        if (character == null)
            return false;

        var save = ProgressionSaveService.Load(character.MetaSaveId);
        if (save == null)
            return false;

        return save.pendingRankUpCelebration
            || (save.unacknowledgedTrialIds != null && save.unacknowledgedTrialIds.Count > 0);
    }

    public static void SetDeferred(bool deferred)
    {
        if (IsDeferred == deferred)
            return;

        IsDeferred = deferred;
        if (!deferred)
            DeferredRefreshRequested?.Invoke();
    }
}
