using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One reward card in the face picker (name, value, rarity, icon).
/// </summary>
public class UIRewardSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image typeIconImage;
    [SerializeField] private Button button;
    [Header("Optional Status Hover Tooltip")]
    [SerializeField] private HoverTooltipTargetUI statusHoverTooltipTarget;

    private DieFaceSO _face;

    private void Awake()
    {
        if (button == null)
            Debug.LogError($"UIRewardSlot on '{gameObject.name}': assign button.");
    }

    public void Bind(DieFaceSO face, System.Action<DieFaceSO> onPicked)
    {
        _face = face;

        if (face == null) return;

        if (nameText != null) nameText.text = face.Title;
        if (descriptionText != null) descriptionText.text = face.Description;
        if (valueText != null) valueText.text = face.value.ToString();
        if (rarityText != null) rarityText.text = face.rarity.ToString();

        var faceSprite = face.uiIcon;
        var elementSprite = GameIconCatalog.GetElementIcon(face.type);
        if (iconImage != null)
        {
            iconImage.sprite = faceSprite;
            iconImage.enabled = faceSprite != null;
        }

        if (typeIconImage != null)
        {
            typeIconImage.sprite = elementSprite;
            typeIconImage.enabled = elementSprite != null;
        }

        SetupStatusHoverTooltip(face);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onPicked != null)
                button.onClick.AddListener(() => onPicked.Invoke(_face));
        }
    }

    /// <summary>For preview-only slots; swap overlay disables the reward button.</summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    /// <summary>Used for average-roll hover on the swap overlay (raycast target).</summary>
    public GameObject GetHoverTarget()
    {
        return button != null ? button.gameObject : gameObject;
    }

    private void SetupStatusHoverTooltip(DieFaceSO face)
    {
        var target = statusHoverTooltipTarget;
        if (target == null)
        {
            var hoverGo = GetHoverTarget();
            if (hoverGo == null) return;
            target = hoverGo.GetComponent<HoverTooltipTargetUI>() ?? hoverGo.AddComponent<HoverTooltipTargetUI>();
            statusHoverTooltipTarget = target;
        }

        BuildEffectTooltip(face, out var title, out var description);
        target.SetContent(title, description);
    }

    private static void BuildEffectTooltip(DieFaceSO face, out string title, out string description)
    {
        title = string.Empty;
        description = string.Empty;
        if (face?.actions == null || face.actions.Count == 0) return;

        var effectNames = new List<string>();
        var descriptions = new List<string>();
        var seenStatuses = new HashSet<StatusEffectSO>();
        var healAdded = false;

        for (var i = 0; i < face.actions.Count; i++)
        {
            var action = face.actions[i];
            if (action is ApplyStatusEffectAction apply)
            {
                var def = apply.StatusEffectDefinition;
                if (def == null || !seenStatuses.Add(def)) continue;

                var effectName = string.IsNullOrWhiteSpace(def.effectName) ? def.name : def.effectName;
                if (!string.IsNullOrWhiteSpace(effectName))
                    effectNames.Add(effectName.Trim());
                if (!string.IsNullOrWhiteSpace(def.description))
                    descriptions.Add(def.description.Trim());
                continue;
            }

            if (action is HealAction heal && !healAdded)
            {
                healAdded = true;
                effectNames.Add("Heal");
                descriptions.Add($"Heals {heal.Amount} HP at turn end.");
            }
        }

        if (effectNames.Count == 0 && descriptions.Count == 0) return;
        title = effectNames.Count > 0 ? string.Join(" · ", effectNames) : "Effect";
        description = descriptions.Count > 0 ? string.Join("\n\n", descriptions) : string.Empty;
    }
}
