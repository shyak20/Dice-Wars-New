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

    [Header("Dynamic Prefabs")]
    public GameObject damageButtonPrefab; // Renamed from attack
    public GameObject armorButtonPrefab;  // Renamed from defense

    [Header("Controls")]
    public Button rollButton;
    public Button endTurnButton;

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

    private Dictionary<DieAssetSO, DiceTrayButtonView> diceButtonViews = new Dictionary<DieAssetSO, DiceTrayButtonView>();
    private Dictionary<DieAssetSO, Button> diceButtons = new Dictionary<DieAssetSO, Button>();
    private List<DieAssetSO> currentlySelected = new List<DieAssetSO>();
    private DieAssetSO tooltipShownForDie;

    private TMP_Text rollButtonText;
    private int rollsRemaining;
    private int maxRolls;

    private void OnEnable()
    {
        if (rollButton != null && rollButtonText == null)
            rollButtonText = rollButton.GetComponentInChildren<TMP_Text>();

        CombatEvents.OnPowerChanged += UpdatePowerUI;
        CombatEvents.OnPoolsUpdated += UpdatePoolsUI;
        CombatEvents.OnBustOccurred += ShowBustPanel;
        CombatEvents.OnStateChanged += HandleStateChange;
        CombatEvents.OnRollsRemainingChanged += UpdateRollsUI;
    }

    private void OnDisable()
    {
        CombatEvents.OnPowerChanged -= UpdatePowerUI;
        CombatEvents.OnPoolsUpdated -= UpdatePoolsUI;
        CombatEvents.OnBustOccurred -= ShowBustPanel;
        CombatEvents.OnStateChanged -= HandleStateChange;
        CombatEvents.OnRollsRemainingChanged -= UpdateRollsUI;
    }

    private void Start()
    {
        if (bustPanel != null) bustPanel.SetActive(false);
        HideDieTooltip();
        HideFaceHoverTooltip();
        if (rollButton != null)
        {
            rollButton.onClick.AddListener(() => CombatEvents.OnRollCommand?.Invoke());
            rollButton.interactable = false;
        }
        if (endTurnButton != null) endTurnButton.onClick.AddListener(() => CombatEvents.OnEndTurnPressed?.Invoke());
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
                diceButtonViews.Add(die, trayView);
            }
            else
                Debug.LogError($"CombatUIController: prefab '{prefab.name}' is missing DiceTrayButtonView. Add the component and assign regular/selected images.");

            Button btn = btnObj.GetComponent<Button>();
            diceButtons[die] = btn;
            btn.onClick.AddListener(() => ToggleSelection(die));
        }
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
        rollButton.interactable = currentlySelected.Count > 0;
    }

    private void ShowDieTooltip(DieAssetSO die)
    {
        if (dieTooltipPanel == null || dieTooltipSlotContainer == null || dieTooltipSlotPrefab == null || die == null)
            return;

        tooltipShownForDie = die;
        dieTooltipPanel.SetActive(true);
        HideFaceHoverTooltip();

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
        exit.callback.AddListener(_ => HideFaceHoverTooltip());
        et.triggers.Add(exit);
    }

    private void ShowFaceHoverTooltip(DieFaceSO face)
    {
        if (faceHoverTooltipPanel == null) return;
        if (faceHoverTitleText != null) faceHoverTitleText.text = face != null ? face.Title : "";
        if (faceHoverDescriptionText != null) faceHoverDescriptionText.text = face != null ? face.Description : "";
        faceHoverTooltipPanel.SetActive(true);
    }

    private void HideFaceHoverTooltip()
    {
        if (faceHoverTitleText != null) faceHoverTitleText.text = "";
        if (faceHoverDescriptionText != null) faceHoverDescriptionText.text = "";
        if (faceHoverTooltipPanel != null) faceHoverTooltipPanel.SetActive(false);
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
        Transform selectedButtonTransform = null;
        if (tooltipShownForDie != null && diceButtons.TryGetValue(tooltipShownForDie, out var btn) && btn != null)
            selectedButtonTransform = btn.transform;

        for (var i = 0; i < hits.Count; i++)
        {
            var hitTransform = hits[i].gameObject != null ? hits[i].gameObject.transform : null;
            if (hitTransform == null) continue;
            if (tooltipTransform != null && hitTransform.IsChildOf(tooltipTransform)) return true;
            if (selectedButtonTransform != null && hitTransform.IsChildOf(selectedButtonTransform)) return true;
        }

        return false;
    }

    private void UpdatePowerUI(int current, int max)
    {
        if (powerSlider != null) { powerSlider.maxValue = max; powerSlider.value = current; }
        if (powerText != null) powerText.text = $"{current} / {max}";
    }

    private void UpdatePoolsUI(Dictionary<DieType, int> pools)
    {
        // UI updated for renamed types
        if (poolText != null)
            poolText.text = $"DMG: {pools[DieType.Damage]} | ARM: {pools[DieType.Armor]}";
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
    }
}