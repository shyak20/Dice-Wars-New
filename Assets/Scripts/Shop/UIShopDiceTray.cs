using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Read-only dice tray for the shop: same tray prefabs and die/face tooltip behavior as <see cref="CombatUIController"/>.
/// Wire the same tooltip panel hierarchy as the fight scene (or a shop copy). Refresh is driven by <see cref="UIShopWindow"/>.
/// </summary>
public class UIShopDiceTray : MonoBehaviour
{
    private static readonly int[] DieTooltipGridTemplate = { -1, 0, -1, -1, 1, 2, 3, 4, -1, 5 };

    [Header("Dice Tray")]
    [SerializeField] private Transform diceButtonContainer;
    [SerializeField] private GameObject damageButtonPrefab;
    [SerializeField] private GameObject armorButtonPrefab;

    [Header("Die Tooltip (match Fight scene)")]
    [SerializeField] private GameObject dieTooltipPanel;
    [SerializeField] private Transform dieTooltipSlotContainer;
    [SerializeField] private GameObject dieTooltipSlotPrefab;
    [Header("Gem slots in die tooltip (optional)")]
    [SerializeField] private Transform dieTooltipGemIconContainer;
    [SerializeField] private DieTooltipGemSlotView dieTooltipGemSlotPrefab;
    [SerializeField] private GameObject faceHoverTooltipPanel;
    [SerializeField] private TMP_Text faceHoverTitleText;
    [SerializeField] private TMP_Text faceHoverDescriptionText;

    [Header("Status hover (face has Apply Status action)")]
    [SerializeField] private GameObject statusHoverTooltipPanel;
    [SerializeField] private TMP_Text statusHoverTitleText;
    [SerializeField] private TMP_Text statusHoverDescriptionText;

    private readonly Dictionary<DieAssetSO, DiceTrayButtonView> _diceButtonViews = new Dictionary<DieAssetSO, DiceTrayButtonView>();
    private readonly Dictionary<DieAssetSO, Button> _diceButtons = new Dictionary<DieAssetSO, Button>();
    private DieAssetSO _tooltipShownForDie;
    private DieAssetSO _pinnedTooltipDie;
    private DieAssetSO _hoveredTooltipDie;

    private void Awake()
    {
        if (diceButtonContainer == null)
            Debug.LogError("UIShopDiceTray: assign diceButtonContainer.", this);
        if (damageButtonPrefab == null || armorButtonPrefab == null)
            Debug.LogError("UIShopDiceTray: assign damage and armor button prefabs (same as CombatUIController).", this);
    }

    private void Update()
    {
        if (dieTooltipPanel == null || !dieTooltipPanel.activeSelf) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (ClickShouldKeepTooltipOpen()) return;

        ClearTraySelectionVisuals();
        _pinnedTooltipDie = null;
        _hoveredTooltipDie = null;
        HideDieTooltip();
    }

    /// <summary>Rebuilds tray from <see cref="PlayerDataSO.currentDeck"/>.</summary>
    public void RebuildFromDeck()
    {
        if (diceButtonContainer == null) return;
        if (PlayerDataContainer.Instance == null || PlayerDataContainer.Instance.RuntimeData == null)
        {
            Debug.LogError("UIShopDiceTray: PlayerDataContainer or RuntimeData missing.", this);
            return;
        }

        foreach (Transform child in diceButtonContainer)
            Destroy(child.gameObject);
        _diceButtonViews.Clear();
        _diceButtons.Clear();
        _pinnedTooltipDie = null;
        _hoveredTooltipDie = null;
        ClearTraySelectionVisuals();
        HideDieTooltip();

        foreach (var die in PlayerDataContainer.Instance.RuntimeData.currentDeck)
        {
            if (die == null) continue;

            var prefab = die.dieType == DieType.Damage ? damageButtonPrefab : armorButtonPrefab;
            if (prefab == null) continue;

            var btnObj = Instantiate(prefab, diceButtonContainer);
            var txt = btnObj.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = die.dieName;

            var trayView = btnObj.GetComponent<DiceTrayButtonView>();
            if (trayView != null)
            {
                trayView.SetIcon(die.uiIcon);
                trayView.SetSelectedIconShakeEnabled(false);
                _diceButtonViews[die] = trayView;
            }
            else
                Debug.LogError($"UIShopDiceTray: prefab '{prefab.name}' needs DiceTrayButtonView.", btnObj);

            var btn = btnObj.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogError($"UIShopDiceTray: prefab '{prefab.name}' needs a Button.", btnObj);
                continue;
            }

            _diceButtons[die] = btn;
            var captured = die;
            btn.onClick.AddListener(() => OnTrayDieClicked(captured));
            RegisterTrayHover(btn, captured);
        }
    }

    private void OnTrayDieClicked(DieAssetSO die)
    {
        if (die == null) return;

        foreach (var kv in _diceButtonViews)
            kv.Value.SetSelected(kv.Key == die);

        _pinnedTooltipDie = die;
        ShowDieTooltip(die);
    }

    private void RegisterTrayHover(Button btn, DieAssetSO die)
    {
        if (btn == null || die == null) return;
        var go = btn.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            _hoveredTooltipDie = die;
            ShowDieTooltip(die);
        });
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            if (_hoveredTooltipDie == die)
                _hoveredTooltipDie = null;
            if (_pinnedTooltipDie != null)
                ShowDieTooltip(_pinnedTooltipDie);
            else
                HideDieTooltip();
        });
        et.triggers.Add(exit);
    }

    private void ClearTraySelectionVisuals()
    {
        foreach (var kv in _diceButtonViews)
            kv.Value.SetSelected(false);
    }

    private void ShowDieTooltip(DieAssetSO die)
    {
        if (dieTooltipPanel == null || dieTooltipSlotContainer == null || dieTooltipSlotPrefab == null || die == null)
            return;

        _tooltipShownForDie = die;
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
                Debug.LogError("UIShopDiceTray: dieTooltipSlotPrefab must include UIRewardSlot.", this);
                Destroy(go);
                continue;
            }

            slot.Bind(face, null);
            slot.SetInteractable(false);
            RegisterFaceHover(slot, face);
        }

        RebuildTooltipGemIcons(die);
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

    private void HideDieTooltip()
    {
        _tooltipShownForDie = null;
        if (dieTooltipPanel != null)
            dieTooltipPanel.SetActive(false);
        HideFaceHoverTooltip();
        HideStatusHoverTooltip();
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

    private bool ClickShouldKeepTooltipOpen()
    {
        if (EventSystem.current == null) return false;

        var pointer = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0) return false;

        var tooltipTransform = dieTooltipPanel != null ? dieTooltipPanel.transform : null;
        var statusTooltipTransform = statusHoverTooltipPanel != null ? statusHoverTooltipPanel.transform : null;
        var trayTransform = diceButtonContainer;

        for (var i = 0; i < hits.Count; i++)
        {
            var hitTransform = hits[i].gameObject != null ? hits[i].gameObject.transform : null;
            if (hitTransform == null) continue;
            if (tooltipTransform != null && hitTransform.IsChildOf(tooltipTransform)) return true;
            if (statusTooltipTransform != null && hitTransform.IsChildOf(statusTooltipTransform)) return true;
            if (trayTransform != null && hitTransform.IsChildOf(trayTransform)) return true;
        }

        return false;
    }
}
