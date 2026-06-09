using System.Collections.Generic;

/// <summary>
/// Thin wrapper over <see cref="ProgressionRewardDisplayResolver"/> for legacy call sites.
/// </summary>
public static class ProgressionTrialRewardRowPresenter
{
    public readonly struct RowViewModel
    {
        public RowViewModel(UnityEngine.Sprite icon, string text)
        {
            Icon = icon;
            Text = text ?? string.Empty;
        }

        public UnityEngine.Sprite Icon { get; }
        public string Text { get; }
        public bool HasContent => Icon != null || !string.IsNullOrWhiteSpace(Text);
    }

    public static void CollectRows(
        ProgressionRewardVisualCatalogSO catalog,
        IReadOnlyList<ProgressionRewardBase> rewards,
        string trialCompletionRowFormatOverride,
        List<RowViewModel> into)
    {
        if (into == null || rewards == null)
            return;

        var entries = new List<ProgressionRewardDisplayEntry>();
        ProgressionRewardDisplayResolver.ExpandRewards(
            rewards,
            catalog,
            trialCompletionRowFormatOverride,
            ProgressionRewardExpandMode.PerReward,
            entries);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!entry.HasContent)
                continue;

            into.Add(new RowViewModel(entry.icon, entry.text));
        }
    }

    public static RowViewModel BuildRow(
        ProgressionRewardVisualCatalogSO catalog,
        ProgressionRewardBase reward,
        string trialRowFormatOverride = null)
    {
        if (reward == null)
            return default;

        var entries = new List<ProgressionRewardDisplayEntry>();
        ProgressionRewardDisplayResolver.ExpandRewards(
            new[] { reward },
            catalog,
            trialRowFormatOverride,
            ProgressionRewardExpandMode.PerReward,
            entries);

        if (entries.Count == 0)
            return default;

        var entry = entries[0];
        return new RowViewModel(entry.icon, entry.text);
    }

    /// <summary>Delegates to <see cref="ProgressionRewardDisplayResolver.FormatItemRowTitle"/>.</summary>
    public static string FormatRelicGemRowTitle(ProgressionRewardBase reward, string itemDisplayName) =>
        ProgressionRewardDisplayResolver.FormatItemRowTitle(reward, itemDisplayName);
}
