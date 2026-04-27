using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [Tooltip("Optional. Behind card content; uses DieFaceSO.uiTooltipBackground.")]
    [SerializeField] private Image tooltipBackgroundImage;
    [SerializeField] private Button button;
    [Header("Optional hover highlight")]
    [Tooltip("Shown while the pointer is over the slot (same raycast target as the Button). Leave empty to disable.")]
    [SerializeField] private GameObject hoverRevealObject;
    [Header("Optional Status Hover Tooltip")]
    [SerializeField] private HoverTooltipTargetUI statusHoverTooltipTarget;

    private HoverTooltipPanelUI _statusHoverTooltipPanel;
    private bool _hoverRevealEnabled = true;
    private DieFaceSO _face;
    public DieFaceSO Face => _face;

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

        if (tooltipBackgroundImage != null)
            DieTooltipBackgrounds.ApplyFaceTooltip(tooltipBackgroundImage, face);

        SetupStatusHoverTooltip(face);

        _hoverRevealEnabled = true;
        if (hoverRevealObject != null)
            hoverRevealObject.SetActive(false);

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

    /// <summary>
    /// When false, pointer hover no longer shows <see cref="hoverRevealObject"/> (and it is hidden immediately).
    /// </summary>
    public void SetHoverRevealEnabled(bool enabled)
    {
        _hoverRevealEnabled = enabled;
        if (!enabled && hoverRevealObject != null)
            hoverRevealObject.SetActive(false);
    }

    /// <summary>Optional explicit panel used by status hover on this slot; falls back to scene lookup when null.</summary>
    public void SetStatusHoverTooltipPanel(HoverTooltipPanelUI panel)
    {
        _statusHoverTooltipPanel = panel;
    }

    private void ApplyHoverRevealPointerEnter()
    {
        if (!_hoverRevealEnabled || hoverRevealObject == null) return;
        hoverRevealObject.SetActive(true);
    }

    private void ApplyHoverRevealPointerExit()
    {
        if (hoverRevealObject == null) return;
        hoverRevealObject.SetActive(false);
    }

    /// <summary>Used for average-roll hover on the swap overlay (raycast target).</summary>
    public GameObject GetHoverTarget()
    {
        return button != null ? button.gameObject : gameObject;
    }

    /// <summary>
    /// Call from code that builds <see cref="EventTrigger"/> PointerEnter/Exit for face tooltips (before <c>triggers.Add</c>).
    /// Adds show/hide for <see cref="hoverRevealObject"/> on the same entries so a later <c>triggers.Clear()</c> is not used for reveal.
    /// </summary>
    public void AppendHoverRevealListeners(EventTrigger.Entry pointerEnter, EventTrigger.Entry pointerExit)
    {
        if (hoverRevealObject == null) return;
        if (pointerEnter != null)
            pointerEnter.callback.AddListener(_ => ApplyHoverRevealPointerEnter());
        if (pointerExit != null)
            pointerExit.callback.AddListener(_ => ApplyHoverRevealPointerExit());
    }

    /// <summary>
    /// When nothing else wires pointer hover on this slot (reward picker, shop face row), registers self-contained enter/exit.
    /// Do not use together with external <c>EventTrigger.triggers.Clear()</c> on the same button unless you also call <see cref="AppendHoverRevealListeners"/>.
    /// </summary>
    public void EnsureStandaloneHoverReveal()
    {
        if (hoverRevealObject == null || button == null) return;
        hoverRevealObject.SetActive(false);
        var go = button.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ApplyHoverRevealPointerEnter());
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ApplyHoverRevealPointerExit());
        et.triggers.Add(enter);
        et.triggers.Add(exit);
    }

    private void SetupStatusHoverTooltip(DieFaceSO face)
    {
        var hoverGo = GetHoverTarget();
        if (hoverGo == null) return;

        // Always bind status hover to the actual raycast target (button). A serialized reference on
        // another child object will never receive pointer enter/exit for this slot.
        var target = statusHoverTooltipTarget;
        if (target == null || target.gameObject != hoverGo)
            target = hoverGo.GetComponent<HoverTooltipTargetUI>() ?? hoverGo.AddComponent<HoverTooltipTargetUI>();
        statusHoverTooltipTarget = target;

        BuildEffectTooltip(face, out var title, out var description);
        if (_statusHoverTooltipPanel != null)
            target.Configure(_statusHoverTooltipPanel, title, description);
        else
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
