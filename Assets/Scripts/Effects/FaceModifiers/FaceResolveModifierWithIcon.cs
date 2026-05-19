using System;
using UnityEngine;

/// <summary>
/// <see cref="FaceResolveModifierBase"/> with <see cref="GameIconIndexSO"/> art and die flyout routing:
/// Activate Immediately → player status bar; deferred → element pool.
/// </summary>
[Serializable]
public abstract class FaceResolveModifierWithIcon : FaceResolveModifierBase
{
    protected abstract ActionVisualId VisualKey { get; }

    public ActionVisualId GetActionVisualId() => VisualKey;

    public Sprite ResolveActionIcon() =>
        VisualKey == ActionVisualId.None ? null : GameIconCatalog.GetActionIcon(VisualKey);

    protected void SetPlayerBarBuffActive(TurnRegistry registry, bool active)
    {
        if (registry == null || VisualKey == ActionVisualId.None)
            return;
        registry.SetPlayerBarBuff(VisualKey, active);
    }

    public void AppendFlyoutContributionIfAny(FaceResult result)
    {
        if (result == null || VisualKey == ActionVisualId.None)
            return;

        var icon = ResolveActionIcon();
        if (icon == null)
            return;

        var immediate = ActivateImmediately;
        result.ActionPoolContributions.Add(new FacePoolExtraContribution
        {
            PoolKey = PoolRowKey.Custom(VisualKey.ToString()),
            Amount = 1,
            Icon = icon,
            PoolRowBackground = GameIconCatalog.GetActionBackground(VisualKey),
            VisualFlyoutOnly = immediate,
            FlyToPlayerStatusBar = immediate,
        });
    }
}
