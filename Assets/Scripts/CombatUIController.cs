using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

public class CombatUIController : MonoBehaviour
{
    private static readonly int[] DieTooltipGridTemplate = { -1, 0, -1, -1, 1, 2, 3, 4, -1, 5 };

    [Header("Dice Tray (The Hand)")]
    public Transform diceButtonContainer;
    public CanvasGroup trayCanvasGroup;
    [Tooltip("Shown while waiting to roll and no dice are selected in the tray.")]
    [SerializeField] private GameObject noDiceSelectedIndicator;

    [Header("Dynamic Prefabs")]
    public GameObject damageButtonPrefab; // Renamed from attack
    public GameObject armorButtonPrefab;  // Renamed from defense

    [Header("Controls")]
    public Button rollButton;
    public Button endTurnButton;
    public Button cheatWinButton;

    [Header("Status Displays")]
    public Slider powerSlider;
    public TMP_Text powerText;
    public TMP_Text poolText;

    [Header("Bust Panel")]
    public GameObject bustPanel;
    public Button nullifyDamageButton; // Renamed from attack
    public Button nullifyArmorButton;  // Renamed from defense

    [Header("Die Tooltip (Fight Scene)")]
    [SerializeField] private GameObject dieTooltipPanel;
    [SerializeField] private Transform dieTooltipSlotContainer;
    [SerializeField] private GameObject dieTooltipSlotPrefab; // Should use UIRewardSlot prefab.
    [SerializeField] private GameObject faceHoverTooltipPanel;
    [SerializeField] private TMP_Text faceHoverTitleText;
    [SerializeField] private TMP_Text faceHoverDescriptionText;

    [Header("Status hover (face has Apply Status action)")]
    [SerializeField] private GameObject statusHoverTooltipPanel;
    [SerializeField] private TMP_Text statusHoverTitleText;
    [SerializeField] private TMP_Text statusHoverDescriptionText;

    private Dictionary<DieAssetSO, DiceTrayButtonView> diceButtonViews = new Dictionary<DieAssetSO, DiceTrayButtonView>();
    private Dictionary<DieAssetSO, Button> diceButtons = new Dictionary<DieAssetSO, Button>();
    private List<DieAssetSO> currentlySelected = new List<DieAssetSO>();
    private DieAssetSO tooltipShownForDie;

    private TMP_Text rollButtonText;
    private int rollsRemaining;
    private int maxRolls;
    private CombatState _combatState = CombatState.WaitingForRoll;

    private void OnEnable()
    {
        if (rollButton != null && rollButtonText == null)
            rollButtonText = rollButton.GetComponentInChildren<TMP_Text>();

        CombatEvents.OnPowerChanged += UpdatePowerUI;
        CombatEvents.OnStoredActionsPoolUpdated += UpdateStoredActionsPoolSummaryText;
        CombatEvents.OnBustOccurred += ShowBustPanel;
        CombatEvents.OnStateChanged += HandleStateChange;
        CombatEvents.OnRollsRemainingChanged += UpdateRollsUI;
        CombatEvents.OnRerollDieSelectionModeChanged += HandleRerollDieSelectionMode;
    }

    private void OnDisable()
    {
        CombatEvents.OnPowerChanged -= UpdatePowerUI;
        CombatEvents.OnStoredActionsPoolUpdated -= UpdateStoredActionsPoolSummaryText;
        CombatEvents.OnBustOccurred -= ShowBustPanel;
        CombatEvents.OnStateChanged -= HandleStateChange;
        CombatEvents.OnRollsRemainingChanged -= UpdateRollsUI;
        CombatEvents.OnRerollDieSelectionModeChanged -= HandleRerollDieSelectionMode;
    }

    private void HandleRerollDieSelectionMode(bool active)
    {
        if (trayCanvasGroup != null)
        {
            trayCanvasGroup.interactable = !active;
            trayCanvasGroup.blocksRaycasts = true;
        }

        if (active)
        {
            if (rollButton != null) rollButton.interactable = false;
            if (endTurnButton != null) endTurnButton.interactable = false;
        }
        else
            HandleStateChange(_combatState);
    }

    private void Start()
    {
        if (bustPanel != null) bustPanel.SetActive(false);
        HideDieTooltip();
        HideFaceHoverTooltip();
        HideStatusHoverTooltip();
        if (rollButton != null)
        {
            rollButton.onClick.AddListener(() => CombatEvents.OnRollCommand?.Invoke());
            rollButton.interactable = false;
        }
        if (endTurnButton != null) endTurnButton.onClick.AddListener(() => CombatEvents.OnEndTurnPressed?.Invoke());
        if (cheatWinButton != null) cheatWinButton.onClick.AddListener(() => CombatEvents.OnCheatWinPressed?.Invoke());
        Invoke(nameof(InitializeDiceButtons), 0.15f);
    }

    public void InitializeDiceButtons()
    {
        if (PlayerDataContainer.Instance == null) return;
        foreach (Transform child in diceButtonContainer) Destroy(child.gameObject);
        diceButtonViews.Clear();
        diceButtons.Clear();
        currentlySelected.Clear();
        HideDieTooltip();

        foreach (DieAssetSO die in PlayerDataContainer.Instance.RuntimeData.currentDeck)
        {
            // Logic updated for renamed types
            GameObject prefab = (die.dieType == DieType.Damage) ? damageButtonPrefab : armorButtonPrefab;
            GameObject btnObj = Instantiate(prefab, diceButtonContainer);

            TMP_Text txt = btnObj.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = die.dieName;

            var trayView = btnObj.GetComponent<DiceTrayButtonView>();
            if (trayView != null)
            {
                trayView.SetIcon(die.uiIcon);
                trayView.SetSelectedIconShakeEnabled(true);
                diceButtonViews.Add(die, trayView);
            }
            else
                Debug.LogError($"CombatUIController: prefab '{prefab.name}' is missing DiceTrayButtonView. Add the component and assign regular/selected images.");

            Button btn = btnObj.GetComponent<Button>();
            diceButtons[die] = btn;
            btn.onClick.AddListener(() => ToggleSelection(die));
        }

        UpdateNoDiceSelectedIndicator();
    }

    private void Update()
    {
        if (dieTooltipPanel == null || !dieTooltipPanel.activeSelf) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (ClickShouldKeepTooltipOpen()) return;

        HideDieTooltip();
    }

    private void ToggleSelection(DieAssetSO die)
    {
        CombatEvents.OnDieToggled?.Invoke(die);
        if (currentlySelected.Contains(die))
        {
            currentlySelected.Remove(die);
            if (diceButtonViews.TryGetValue(die, out var view))
                view.SetSelected(false);

            if (tooltipShownForDie == die)
                HideDieTooltip();
        }
        else
        {
            currentlySelected.Add(die);
            if (diceButtonViews.TryGetValue(die, out var view))
                view.SetSelected(true);

            ShowDieTooltip(die);
        }
        if (rollButton != null)
            rollButton.interactable = currentlySelected.Count > 0;
        UpdateNoDiceSelectedIndicator();
    }

    private void UpdateNoDiceSelectedIndicator()
    {
        if (noDiceSelectedIndicator == null)
            return;
        noDiceSelectedIndicator.SetActive(_combatState == CombatState.WaitingForRoll && currentlySelected.Count == 0);
    }

    private void ShowDieTooltip(DieAssetSO die)
    {
        if (dieTooltipPanel == null || dieTooltipSlotContainer == null || dieTooltipSlotPrefab == null || die == null)
            return;

        tooltipShownForDie = die;
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
                Debug.LogError("CombatUIController: dieTooltipSlotPrefab must include UIRewardSlot.");
                Destroy(go);
                continue;
            }

            slot.Bind(face, null);
            slot.SetInteractable(false);
            RegisterFaceHover(slot, face);
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
        tooltipShownForDie = null;
        if (dieTooltipPanel != null)
            dieTooltipPanel.SetActive(false);
        HideFaceHoverTooltip();
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
        if (hits.Count == 0) return false; // Clicked non-UI world area.

        var tooltipTransform = dieTooltipPanel != null ? dieTooltipPanel.transform : null;
        var statusTooltipTransform = statusHoverTooltipPanel != null ? statusHoverTooltipPanel.transform : null;
        Transform selectedButtonTransform = null;
        if (tooltipShownForDie != null && diceButtons.TryGetValue(tooltipShownForDie, out var btn) && btn != null)
            selectedButtonTransform = btn.transform;

        for (var i = 0; i < hits.Count; i++)
        {
            var hitTransform = hits[i].gameObject != null ? hits[i].gameObject.transform : null;
            if (hitTransform == null) continue;
            if (tooltipTransform != null && hitTransform.IsChildOf(tooltipTransform)) return true;
            if (statusTooltipTransform != null && hitTransform.IsChildOf(statusTooltipTransform)) return true;
            if (selectedButtonTransform != null && hitTransform.IsChildOf(selectedButtonTransform)) return true;
        }

        return false;
    }

    private void UpdatePowerUI(int current, int max)
    {
        if (powerSlider != null) { powerSlider.maxValue = max; powerSlider.value = current; }
        if (powerText != null) powerText.text = $"{current} / {max}";
    }

    private void UpdateStoredActionsPoolSummaryText(Dictionary<PoolRowKey, int> pools)
    {
        if (poolText == null) return;
        if (pools == null || pools.Count == 0)
        {
            poolText.text = "";
            return;
        }

        var keys = new System.Collections.Generic.List<PoolRowKey>(pools.Keys);
        keys.Sort((a, b) => PoolRowKey.Compare(a, b));
        var parts = new System.Collections.Generic.List<string>();
        foreach (var k in keys)
        {
            if (!pools.TryGetValue(k, out var n) || n < 1) continue;
            parts.Add($"{k.DisplayLabel} {n}");
        }

        poolText.text = string.Join("  |  ", parts);
    }

    private void ShowBustPanel(int currentDmg, int currentArm)
    {
        if (bustPanel == null) return;
        bustPanel.SetActive(true);

        nullifyDamageButton.gameObject.SetActive(currentDmg > 0);
        nullifyArmorButton.gameObject.SetActive(currentArm > 0);

        nullifyDamageButton.onClick.RemoveAllListeners();
        nullifyArmorButton.onClick.RemoveAllListeners();

        nullifyDamageButton.onClick.AddListener(() => { CombatEvents.OnBustResolved?.Invoke(true); bustPanel.SetActive(false); });
        nullifyArmorButton.onClick.AddListener(() => { CombatEvents.OnBustResolved?.Invoke(false); bustPanel.SetActive(false); });
    }

    private void UpdateRollsUI(int remaining, int max)
    {
        rollsRemaining = remaining;
        maxRolls = max;
        if (rollButtonText != null) rollButtonText.text = $"Roll!\n{remaining}/{max}";
    }

    private void HandleStateChange(CombatState state)
    {
        _combatState = state;
        bool isWaiting = (state == CombatState.WaitingForRoll);
        bool isRolling = (state == CombatState.Rolling);

        if (trayCanvasGroup != null)
        {
            // Hide the tray while a roll is being resolved, then show it again afterward.
            trayCanvasGroup.gameObject.SetActive(!isRolling);
            trayCanvasGroup.interactable = isWaiting;
            trayCanvasGroup.blocksRaycasts = isWaiting;
            trayCanvasGroup.alpha = isWaiting ? 1f : 0.5f;
        }
        if (!isWaiting)
            HideDieTooltip();
        if (rollButton != null) { rollButton.gameObject.SetActive(isWaiting); if (isWaiting) rollButton.interactable = currentlySelected.Count > 0; }
        if (endTurnButton != null) { bool showEndTurn = isWaiting && rollsRemaining > 0; endTurnButton.gameObject.SetActive(showEndTurn); endTurnButton.interactable = showEndTurn; }
        UpdateNoDiceSelectedIndicator();
    }
}