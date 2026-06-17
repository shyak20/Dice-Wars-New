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
        var seenStatuses = new HashSet<StatusEffectSO>();
        var seenActionVisualIds = new HashSet<ActionVisualId>();

        void TryAppendActionVisual(ActionVisualId id)
        {
            if (id == ActionVisualId.None)
                return;
            if (!seenActionVisualIds.Add(id))
                return;
            if (!GameIconCatalog.TryGetActionTooltip(id, out var catalogTitle, out var catalogDesc))
                return;

            var namePart = !string.IsNullOrWhiteSpace(catalogTitle) ? catalogTitle.Trim() : (string)null;
            var descPart = !string.IsNullOrWhiteSpace(catalogDesc) ? catalogDesc.Trim() : (string)null;
            if (namePart == null && descPart == null)
                return;

            if (namePart == null)
                namePart = id.ToString();

            effectNames.Add(namePart);
            if (descPart != null)
                descriptions.Add(descPart);
        }

        for (var i = 0; i < face.actions.Count; i++)
        {
            var action = face.actions[i];
            if (action is ApplyStatusEffectAction apply)
            {
                var def = apply.StatusEffectDefinition;
                if (def == null || !seenStatuses.Add(def))
                    continue;
                if (GameIconCatalog.GetStatusIcon(def) == null)
                    continue;

                var effectName = string.IsNullOrWhiteSpace(def.effectName) ? def.name : def.effectName;
                if (!string.IsNullOrWhiteSpace(effectName))
                    effectNames.Add(effectName.Trim());
                if (!string.IsNullOrWhiteSpace(def.description))
                    descriptions.Add(def.description.Trim());
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
}
