using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Generic die tooltip presenter (faces grid + gem sockets + face/gem/status hover texts).
/// Reusable across fight, shop, rewards, and other screens.
/// </summary>
public sealed class DieTooltipOverlayUI : MonoBehaviour
{
    private static readonly int[] DieTooltipGridTemplate = { -1, 0, -1, -1, 1, 2, 3, 4, -1, 5 };

    [Header("Die Tooltip")]
    [SerializeField] private GameObject dieTooltipPanel;
    [SerializeField] private Transform dieTooltipSlotContainer;
    [SerializeField] private GameObject dieTooltipSlotPrefab;

    [Header("Gem slots (optional)")]
    [SerializeField] private Transform dieTooltipGemIconContainer;
    [SerializeField] private DieTooltipGemSlotView dieTooltipGemSlotPrefab;

    [Header("Face/Gem hover")]
    [SerializeField] private GameObject faceHoverTooltipPanel;
    [SerializeField] private TMP_Text faceHoverTitleText;
    [SerializeField] private TMP_Text faceHoverDescriptionText;

    [Header("Status hover (from face actions)")]
    [SerializeField] private GameObject statusHoverTooltipPanel;
    [SerializeField] private TMP_Text statusHoverTitleText;
    [SerializeField] private TMP_Text statusHoverDescriptionText;

    public DieAssetSO CurrentDie { get; private set; }

    public void ShowDie(DieAssetSO die, bool facesInteractable, Action<int, DieFaceSO> onFaceClicked = null)
    {
        if (dieTooltipPanel == null || dieTooltipSlotContainer == null || dieTooltipSlotPrefab == null || die == null)
            return;

        CurrentDie = die;
        dieTooltipPanel.SetActive(true);
        HideFaceHoverTooltip();
        HideStatusHoverTooltip();

        foreach (Transform child in dieTooltipSlotContainer)
            Destroy(child.gameObject);

        if (die.faces == null || die.faces.Length == 0) return;
        for (var i = 0; i < DieTooltipGridTemplate.Length; i++)
        {
            var faceIndex = DieTooltipGridTemplate[i];
            if (faceIndex < 0 || faceIndex >= die.faces.Length)
            {
                CreateTooltipSpacer();
                continue;
            }

            var face = die.faces[faceIndex];
            if (face == null)
            {
                CreateTooltipSpacer();
                continue;
            }

            var go = Instantiate(dieTooltipSlotPrefab, dieTooltipSlotContainer);
            var slot = go.GetComponent<UIRewardSlot>();
            if (slot == null)
            {
                Debug.LogError("DieTooltipOverlayUI: dieTooltipSlotPrefab must include UIRewardSlot.", this);
                Destroy(go);
                continue;
            }

            if (facesInteractable && onFaceClicked != null)
                slot.Bind(face, _ => onFaceClicked.Invoke(faceIndex, face));
            else
                slot.Bind(face, null);

            slot.SetInteractable(facesInteractable && onFaceClicked != null);
            RegisterFaceHover(slot, face);
        }

        RebuildTooltipGemIcons(die);
    }

    public void Hide()
    {
        CurrentDie = null;
        if (dieTooltipPanel != null)
            dieTooltipPanel.SetActive(false);
        HideFaceHoverTooltip();
        HideStatusHoverTooltip();
    }

    private void RebuildTooltipGemIcons(DieAssetSO die)
    {
        if (dieTooltipGemIconContainer == null || dieTooltipGemSlotPrefab == null || die == null)
            return;

        foreach (Transform child in dieTooltipGemIconContainer)
            Destroy(child.gameObject);

        for (var i = 0; i < DieAssetSO.GemSocketCount; i++)
        {
            var gem = die.GetSocketedGemAt(i);
            var view = Instantiate(dieTooltipGemSlotPrefab, dieTooltipGemIconContainer);
            view.Bind(gem);
            view.transform.localScale = Vector3.one;
            RegisterGemHover(view, gem);
        }
    }

    private void CreateTooltipSpacer()
    {
        var spacer = new GameObject("Empty", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(dieTooltipSlotContainer, false);

        var slotRect = dieTooltipSlotPrefab.transform as RectTransform;
        var spacerRect = spacer.transform as RectTransform;
        var layout = spacer.GetComponent<LayoutElement>();

        if (layout != null && slotRect != null)
        {
            var w = slotRect.rect.width;
            var h = slotRect.rect.height;
            if (w > 0f) layout.preferredWidth = w;
            if (h > 0f) layout.preferredHeight = h;
        }

        if (spacerRect != null)
            spacerRect.localScale = Vector3.one;
    }

    private void RegisterGemHover(DieTooltipGemSlotView slotView, GemSO gem)
    {
        if (slotView == null || gem == null) return;

        var go = slotView.GetHoverTarget();
        if (go == null) return;

        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowGemHoverTooltip(gem));
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideFaceHoverTooltip());
        et.triggers.Add(exit);
    }

    private void ShowGemHoverTooltip(GemSO gem)
    {
        if (faceHoverTooltipPanel == null) return;
        if (faceHoverTitleText != null)
            faceHoverTitleText.text = gem != null ? gem.DisplayLabel : "";
        if (faceHoverDescriptionText != null)
            faceHoverDescriptionText.text = gem != null ? gem.description : "";
        faceHoverTooltipPanel.SetActive(true);
        HideStatusHoverTooltip();
    }

    private void RegisterFaceHover(UIRewardSlot slot, DieFaceSO face)
    {
        if (slot == null || face == null) return;

        var go = slot.GetHoverTarget();
        if (go == null) return;

        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        et.triggers.Clear();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowFaceHoverTooltip(face));
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            HideFaceHoverTooltip();
            HideStatusHoverTooltip();
        });
        et.triggers.Add(exit);
    }

    private void ShowFaceHoverTooltip(DieFaceSO face)
    {
        if (faceHoverTooltipPanel != null)
        {
            if (faceHoverTitleText != null) faceHoverTitleText.text = face != null ? face.Title : "";
            if (faceHoverDescriptionText != null) faceHoverDescriptionText.text = face != null ? face.Description : "";
            faceHoverTooltipPanel.SetActive(true);
        }

        var statusDefs = CollectUniqueStatusEffectsFromFace(face);
        if (statusDefs.Count > 0)
            ShowStatusHoverTooltip(statusDefs);
        else
            HideStatusHoverTooltip();
    }

    private void HideFaceHoverTooltip()
    {
        if (faceHoverTitleText != null) faceHoverTitleText.text = "";
        if (faceHoverDescriptionText != null) faceHoverDescriptionText.text = "";
        if (faceHoverTooltipPanel != null) faceHoverTooltipPanel.SetActive(false);
    }

    private void ShowStatusHoverTooltip(IReadOnlyList<StatusEffectSO> definitions)
    {
        if (statusHoverTooltipPanel == null || definitions == null || definitions.Count == 0) return;

        var titleParts = new List<string>();
        var descParts = new List<string>();
        for (var i = 0; i < definitions.Count; i++)
        {
            var d = definitions[i];
            if (d == null) continue;
            if (!string.IsNullOrWhiteSpace(d.effectName))
                titleParts.Add(d.effectName.Trim());
            if (!string.IsNullOrWhiteSpace(d.description))
                descParts.Add(d.description.Trim());
        }

        if (statusHoverTitleText != null)
            statusHoverTitleText.text = titleParts.Count > 0 ? string.Join(" · ", titleParts) : "";
        if (statusHoverDescriptionText != null)
            statusHoverDescriptionText.text = descParts.Count > 0 ? string.Join("\n\n", descParts) : "";

        statusHoverTooltipPanel.SetActive(true);
    }

    private void HideStatusHoverTooltip()
    {
        if (statusHoverTitleText != null) statusHoverTitleText.text = "";
        if (statusHoverDescriptionText != null) statusHoverDescriptionText.text = "";
        if (statusHoverTooltipPanel != null) statusHoverTooltipPanel.SetActive(false);
    }

    private static List<StatusEffectSO> CollectUniqueStatusEffectsFromFace(DieFaceSO face)
    {
        var result = new List<StatusEffectSO>();
        if (face?.actions == null || face.actions.Count == 0) return result;

        var seen = new HashSet<StatusEffectSO>();
        for (var i = 0; i < face.actions.Count; i++)
        {
            if (face.actions[i] is not ApplyStatusEffectAction apply) continue;
            var def = apply.StatusEffectDefinition;
            if (def == null) continue;
            if (!seen.Add(def)) continue;
            result.Add(def);
        }

        return result;
    }
}
