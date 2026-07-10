using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds hover tooltip text for a <see cref="DieFaceSO"/> only from data that is represented in
/// <see cref="GameIconIndexSO"/> via <see cref="GameIconCatalog"/>: action rows with explicit index title/description,
/// and apply-status rows whose effect has a registered status icon. Base face title/description on the asset are ignored
/// (the card / die tooltip already shows those).
/// </summary>
public static class DieFaceGameIconOnlyTooltipText
{
    /// <summary>Returns false when there is nothing index-backed to show.</summary>
    public static bool TryBuild(DieFaceSO face, out string title, out string description)
    {
        title = string.Empty;
        description = string.Empty;
        if (face?.actions == null || face.actions.Count == 0)
            return false;

        var effectNames = new List<string>();
        var descriptions = new List<string>();
        var seenActionKeys = new HashSet<string>();
        var seenTitleParts = new HashSet<string>();
        var seenDescriptionParts = new HashSet<string>();

        void TryAddTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var trimmed = text.Trim();
            if (!seenTitleParts.Add(trimmed))
                return;

            effectNames.Add(trimmed);
        }

        void TryAddDescription(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var trimmed = text.Trim();
            if (!seenDescriptionParts.Add(trimmed))
                return;

            descriptions.Add(trimmed);
        }

        void TryAppendActionVisual(ActionVisualId id)
        {
            if (id == ActionVisualId.None)
                return;
            if (!GameIconCatalog.TryGetActionTooltip(id, out var catalogTitle, out var catalogDesc))
                return;

            var namePart = !string.IsNullOrWhiteSpace(catalogTitle) ? catalogTitle.Trim() : (string)null;
            var descPart = !string.IsNullOrWhiteSpace(catalogDesc) ? catalogDesc.Trim() : (string)null;
            if (namePart == null && descPart == null)
                return;

            if (namePart == null)
                namePart = id.ToString();

            TryAddTitle(namePart);
            if (descPart != null)
                TryAddDescription(descPart);
        }

        for (var i = 0; i < face.actions.Count; i++)
        {
            var action = face.actions[i];
            if (action == null)
                continue;

            if (!seenActionKeys.Add(GetActionDedupKey(action)))
                continue;

            if (action is ApplyStatusEffectAction apply)
            {
                var def = apply.StatusEffectDefinition;
                if (def == null || GameIconCatalog.GetStatusIcon(def) == null)
                    continue;

                var effectName = string.IsNullOrWhiteSpace(def.effectName) ? def.name : def.effectName;
                TryAddTitle(effectName);
                TryAddDescription(def.description);
                continue;
            }

            if (action is ApplyBenefitToMainEnemyAction benefit && benefit.IsStatusOnlyBenefit)
            {
                var def = benefit.StatusEffectDefinition;
                if (def == null || GameIconCatalog.GetStatusIcon(def) == null)
                    continue;

                var effectName = string.IsNullOrWhiteSpace(def.effectName) ? def.name : def.effectName;
                TryAddTitle(effectName);
                TryAddDescription(def.description);
                continue;
            }

            if (action is GameActionWithIcon gai)
            {
                TryAppendActionVisual(gai.GetActionVisualId());
                continue;
            }

            if (action is FaceResolveModifierWithIcon modWithIcon)
                TryAppendActionVisual(modWithIcon.GetActionVisualId());
        }

        if (effectNames.Count == 0 && descriptions.Count == 0)
            return false;

        title = effectNames.Count > 0 ? string.Join(" · ", effectNames) : "Effect";
        description = descriptions.Count > 0 ? string.Join("\n\n", descriptions) : string.Empty;
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
