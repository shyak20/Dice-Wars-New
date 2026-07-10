using System.Collections.Generic;

/// <summary>
/// Builds status/action explanation entries for a <see cref="DieFaceSO"/> only from data that is represented in
/// <see cref="GameIconIndexSO"/> via <see cref="GameIconCatalog"/>: action rows with explicit index title/description,
/// and apply-status rows whose effect has a registered status icon. Base face title/description on the asset are ignored
/// (the main tooltip / card already shows those). Duplicate actions (e.g. burn + burn) produce a single entry.
/// </summary>
public static class DieFaceGameIconOnlyTooltipText
{
    /// <summary>
    /// Appends one <see cref="TooltipContent"/> per unique status/action on the face. Returns false when
    /// there is nothing index-backed to show.
    /// </summary>
    public static bool TryBuildEntries(DieFaceSO face, List<TooltipContent> results)
    {
        if (results == null || face?.actions == null || face.actions.Count == 0)
            return false;

        var startCount = results.Count;
        var seenActionKeys = new HashSet<string>();
        var seenTitles = new HashSet<string>();

        void TryAddEntry(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
                return;

            var trimmedTitle = title?.Trim() ?? string.Empty;
            if (trimmedTitle.Length > 0 && !seenTitles.Add(trimmedTitle))
                return;

            results.Add(new TooltipContent(trimmedTitle, description?.Trim()));
        }

        void TryAddStatusEntry(StatusEffectSO def)
        {
            if (def == null || GameIconCatalog.GetStatusIcon(def) == null)
                return;

            var effectName = string.IsNullOrWhiteSpace(def.effectName) ? def.name : def.effectName;
            TryAddEntry(effectName, def.description);
        }

        void TryAddActionVisualEntry(ActionVisualId id)
        {
            if (id == ActionVisualId.None)
                return;
            if (!GameIconCatalog.TryGetActionTooltip(id, out var catalogTitle, out var catalogDesc))
                return;

            var namePart = !string.IsNullOrWhiteSpace(catalogTitle) ? catalogTitle : id.ToString();
            TryAddEntry(namePart, catalogDesc);
        }

        for (var i = 0; i < face.actions.Count; i++)
        {
            var action = face.actions[i];
            if (action == null)
                continue;

            if (!seenActionKeys.Add(GetActionDedupKey(action)))
                continue;

            switch (action)
            {
                case ApplyStatusEffectAction apply:
                    TryAddStatusEntry(apply.StatusEffectDefinition);
                    break;
                case ApplyBenefitToMainEnemyAction benefit when benefit.IsStatusOnlyBenefit:
                    TryAddStatusEntry(benefit.StatusEffectDefinition);
                    break;
                case GameActionWithIcon gai:
                    TryAddActionVisualEntry(gai.GetActionVisualId());
                    break;
                case FaceResolveModifierWithIcon modWithIcon:
                    TryAddActionVisualEntry(modWithIcon.GetActionVisualId());
                    break;
            }
        }

        return results.Count > startCount;
    }

    /// <summary>Joined single-block form of <see cref="TryBuildEntries"/>. Returns false when there is nothing to show.</summary>
    public static bool TryBuild(DieFaceSO face, out string title, out string description)
    {
        var entries = new List<TooltipContent>();
        if (!TryBuildEntries(face, entries))
        {
            title = string.Empty;
            description = string.Empty;
            return false;
        }

        var joined = TooltipContentResolver.JoinEntries(entries);
        title = joined.Title;
        description = joined.Description;
        return true;
    }

    static string GetActionDedupKey(IGameAction action)
    {
        if (action is ApplyStatusEffectAction apply && apply.StatusEffectDefinition != null)
            return $"status:{apply.StatusEffectDefinition.GetInstanceID()}";

        if (action is ApplyBenefitToMainEnemyAction benefit && benefit.IsStatusOnlyBenefit && benefit.StatusEffectDefinition != null)
            return $"status:{benefit.StatusEffectDefinition.GetInstanceID()}";

        if (action is GameActionWithIcon gai)
        {
            var id = gai.GetActionVisualId();
            return id != ActionVisualId.None ? $"visual:{id}" : $"type:{action.GetType().FullName}";
        }

        if (action is FaceResolveModifierWithIcon mod)
        {
            var id = mod.GetActionVisualId();
            return id != ActionVisualId.None ? $"visual:{id}" : $"type:{action.GetType().FullName}";
        }

        return $"type:{action.GetType().FullName}";
    }
}
