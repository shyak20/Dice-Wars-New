using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

public class CombatUIController : MonoBehaviour
{

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
    [Tooltip("Selects every die in the tray and rolls them.")]
    [SerializeField] private Button rollAllButton;
    [SerializeField] private CombatManager combatManager;
    public Button endTurnButton;
    public Button cheatWinButton;
    public Button cheatPerfectStrikeButton;

    [Header("Status Displays")]
    public Slider powerSlider;
    public TMP_Text powerText;
    public TMP_Text poolText;

    [Header("Die Tooltip (Fight Scene)")]
    [Tooltip("Presents the die tooltip (face grid + gem sockets). Face/gem hover text goes through HoverTooltipManager.")]
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;

    private Dictionary<DieAssetSO, DiceTrayButtonView> diceButtonViews = new Dictionary<DieAssetSO, DiceTrayButtonView>();
    private Dictionary<DieAssetSO, Button> diceButtons = new Dictionary<DieAssetSO, Button>();
    private List<DieAssetSO> currentlySelected = new List<DieAssetSO>();
    private DieAssetSO tooltipShownForDie;
    private DieAssetSO pinnedTooltipDie;
    private DieAssetSO hoveredTooltipDie;

    private TMP_Text rollButtonText;
    private int rollsRemaining;
    private int maxRolls;
    private CombatState _combatState = CombatState.WaitingForRoll;
    private bool _rerollSelectionActive;

    private void OnEnable()
    {
        if (rollButton != null && rollButtonText == null)
            rollButtonText = rollButton.GetComponentInChildren<TMP_Text>();

        CombatEvents.OnPowerChanged += UpdatePowerUI;
        CombatEvents.OnStoredActionsPoolUpdated += UpdateStoredActionsPoolSummaryText;
        CombatEvents.OnStateChanged += HandleStateChange;
        CombatEvents.OnRollsRemainingChanged += UpdateRollsUI;
        CombatEvents.OnRerollDieSelectionModeChanged += HandleRerollDieSelectionMode;
        CombatEvents.OnCombatSessionInitialized += HandleCombatSessionInitialized;
    }

    private void OnDisable()
    {
        CombatEvents.OnPowerChanged -= UpdatePowerUI;
        CombatEvents.OnStoredActionsPoolUpdated -= UpdateStoredActionsPoolSummaryText;
        CombatEvents.OnStateChanged -= HandleStateChange;
        CombatEvents.OnRollsRemainingChanged -= UpdateRollsUI;
        CombatEvents.OnRerollDieSelectionModeChanged -= HandleRerollDieSelectionMode;
        CombatEvents.OnCombatSessionInitialized -= HandleCombatSessionInitialized;
    }

    private void HandleCombatSessionInitialized()
    {
        CancelInvoke(nameof(InitializeDiceButtons));
        InitializeDiceButtons();
        RefreshRollControlsVisibility();
    }

    private void HandleRerollDieSelectionMode(bool active)
    {
        _rerollSelectionActive = active;

        if (active)
        {
            if (rollButton != null) rollButton.interactable = false;
            if (rollAllButton != null) rollAllButton.interactable = false;
            if (endTurnButton != null) endTurnButton.interactable = false;
        }
        else
            HandleStateChange(_combatState);

        RefreshRollControlsVisibility();
    }

    private void Start()
    {
        if (dieTooltipOverlay == null)
            Debug.LogError("CombatUIController: assign dieTooltipOverlay (DieTooltipOverlayUI) — the legacy embedded die tooltip panels were removed.", this);
        HideDieTooltip();
        if (rollButton != null)
        {
            rollButton.onClick.AddListener(() => CombatEvents.OnRollCommand?.Invoke());
            rollButton.interactable = false;
        }
        if (rollAllButton != null)
        {
            rollAllButton.onClick.AddListener(RollAllDice);
            rollAllButton.interactable = false;
        }
        if (combatManager == null)
            combatManager = FindObjectOfType<CombatManager>();
        EnsureRollCastOddsTooltips();
        if (endTurnButton != null) endTurnButton.onClick.AddListener(() => CombatEvents.OnEndTurnPressed?.Invoke());
        if (cheatWinButton != null) cheatWinButton.onClick.AddListener(() => CombatEvents.OnCheatWinPressed?.Invoke());
        if (cheatPerfectStrikeButton != null) cheatPerfectStrikeButton.onClick.AddListener(() => CombatEvents.OnCheatPerfectStrikePressed?.Invoke());
        Invoke(nameof(InitializeDiceButtons), 0.15f);
    }

    public void InitializeDiceButtons()
    {
        if (PlayerDataContainer.Instance == null) return;
        foreach (Transform child in diceButtonContainer) Destroy(child.gameObject);
        diceButtonViews.Clear();
        diceButtons.Clear();
        currentlySelected.Clear();
        pinnedTooltipDie = null;
        hoveredTooltipDie = null;
        HideDieTooltip();

        foreach (DieAssetSO die in PlayerDataContainer.Instance.RuntimeData.currentDeck)
        {
            // Logic updated for renamed types
            GameObject prefab = die.dieType == DieType.Damage || die.dieType == DieType.Curse
                ? damageButtonPrefab
                : armorButtonPrefab;
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
            RegisterTrayHover(btn, die);
        }

        RefreshRollButtonsInteractable();
    }

    private void Update()
    {
        HandleFightKeyboardShortcuts();

        if (tooltipShownForDie == null) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (ClickShouldKeepTooltipOpen()) return;

        pinnedTooltipDie = null;
        hoveredTooltipDie = null;
        HideDieTooltip();
    }

    /// <summary>Number keys 1–9 toggle tray dice (deck order); Space rolls; Enter ends turn.</summary>
    void HandleFightKeyboardShortcuts()
    {
        if (!AreRollControlsVisible() || _rerollSelectionActive)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryRollFromKeyboard();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            TryEndTurnFromKeyboard();
            return;
        }

        for (var i = 0; i < 9; i++)
        {
            if (!WasNumberKeyPressed(i))
                continue;

            TryToggleTrayDieByIndex(i);
            break;
        }
    }

    static bool WasNumberKeyPressed(int index)
    {
        if (index < 0 || index > 8)
            return false;

        var digit = KeyCode.Alpha1 + index;
        var keypad = KeyCode.Keypad1 + index;
        return Input.GetKeyDown(digit) || Input.GetKeyDown(keypad);
    }

    void TryToggleTrayDieByIndex(int index)
    {
        var trayDice = GetTrayDiceInDisplayOrder();
        if (index < 0 || index >= trayDice.Count)
            return;

        ToggleSelection(trayDice[index]);
    }

    void TryRollFromKeyboard()
    {
        if (rollButton == null || !rollButton.interactable)
            return;

        CombatEvents.OnRollCommand?.Invoke();
    }

    void TryEndTurnFromKeyboard()
    {
        if (endTurnButton == null || !endTurnButton.interactable)
            return;

        CombatEvents.OnEndTurnPressed?.Invoke();
    }

    List<DieAssetSO> GetTrayDiceInDisplayOrder()
    {
        var result = new List<DieAssetSO>();
        var deck = PlayerDataContainer.Instance?.RuntimeData?.currentDeck;
        if (deck == null)
            return result;

        foreach (var die in deck)
        {
            if (die != null && diceButtons.ContainsKey(die))
                result.Add(die);
        }

        return result;
    }

    /// <summary>Selected tray dice used for pre-roll Perfect Cast / Cast Overload odds on the Roll button.</summary>
    public bool TryGetRollCastOddsInput(out IReadOnlyList<DieAssetSO> selectedDice)
    {
        selectedDice = null;
        if (_combatState != CombatState.WaitingForRoll || currentlySelected.Count == 0)
            return false;

        selectedDice = currentlySelected;
        return true;
    }

    /// <summary>All tray dice — used for Roll All cast-odds tooltip.</summary>
    public bool TryGetRollAllCastOddsInput(out IReadOnlyList<DieAssetSO> allTrayDice)
    {
        allTrayDice = null;
        if (_combatState != CombatState.WaitingForRoll || diceButtons.Count == 0)
            return false;

        var deck = PlayerDataContainer.Instance?.RuntimeData?.currentDeck;
        if (deck == null)
            return false;

        var trayDice = new List<DieAssetSO>();
        foreach (var die in deck)
        {
            if (die != null && diceButtons.ContainsKey(die))
                trayDice.Add(die);
        }

        if (trayDice.Count == 0)
            return false;

        allTrayDice = trayDice;
        return true;
    }

    /// <summary>
    /// Ensures tray buttons exist (deck order). Safe to call from tutorial when sorting targets
    /// need a runtime die slot before <see cref="Start"/>'s delayed init has run.
    /// </summary>
    public void EnsureDiceButtonsReady()
    {
        if (diceButtonContainer == null)
        {
            Debug.LogError("CombatUIController: assign diceButtonContainer.", this);
            return;
        }

        if (diceButtons.Count > 0)
            return;

        CancelInvoke(nameof(InitializeDiceButtons));
        InitializeDiceButtons();
    }

    /// <summary>
    /// Tray die button at <paramref name="trayIndex"/> in deck / display order (0 = first die).
    /// Call <see cref="EnsureDiceButtonsReady"/> first when resolving tutorial highlights.
    /// </summary>
    public bool TryGetTrayDieButtonAt(int trayIndex, out GameObject buttonRoot)
    {
        buttonRoot = null;
        if (trayIndex < 0)
            return false;

        var order = GetTrayDiceInDisplayOrder();
        if (trayIndex >= order.Count)
            return false;

        if (!diceButtons.TryGetValue(order[trayIndex], out var button) || button == null)
            return false;

        buttonRoot = button.gameObject;
        return true;
    }

    private void EnsureRollCastOddsTooltips()
    {
        EnsureRollCastOddsTooltip(rollButton, RollButtonCastOddsHoverTooltip.CastOddsDiceSource.CurrentSelection);
        EnsureRollCastOddsTooltip(rollAllButton, RollButtonCastOddsHoverTooltip.CastOddsDiceSource.AllTrayDice);
    }

    private void EnsureRollCastOddsTooltip(Button button, RollButtonCastOddsHoverTooltip.CastOddsDiceSource diceSource)
    {
        if (button == null)
            return;

        var tooltip = button.GetComponent<RollButtonCastOddsHoverTooltip>();
        if (tooltip == null)
            tooltip = button.gameObject.AddComponent<RollButtonCastOddsHoverTooltip>();

        tooltip.Configure(diceSource, combatManager, this);
    }

    private void ToggleSelection(DieAssetSO die)
    {
        CombatEvents.OnDieToggled?.Invoke(die);
        if (currentlySelected.Contains(die))
        {
            currentlySelected.Remove(die);
            if (diceButtonViews.TryGetValue(die, out var view))
                view.SetSelected(false);
            if (pinnedTooltipDie == die)
                pinnedTooltipDie = null;
            if (hoveredTooltipDie != null)
                ShowDieTooltip(hoveredTooltipDie);
            else if (currentlySelected.Count > 0)
            {
                pinnedTooltipDie = currentlySelected[currentlySelected.Count - 1];
                ShowDieTooltip(pinnedTooltipDie);
            }
            else
                HideDieTooltip();
        }
        else
        {
            currentlySelected.Add(die);
            if (diceButtonViews.TryGetValue(die, out var view))
                view.SetSelected(true);
            PinTooltipToDie(die);
        }
        RefreshRollButtonsInteractable();
    }

    private void RollAllDice()
    {
        if (_combatState != CombatState.WaitingForRoll)
            return;

        SelectAllTrayDice();
        if (currentlySelected.Count == 0)
            return;

        CombatEvents.OnRollCommand?.Invoke();
    }

    private void SelectAllTrayDice()
    {
        var deck = PlayerDataContainer.Instance?.RuntimeData?.currentDeck;
        if (deck == null)
            return;

        foreach (var die in deck)
        {
            if (die == null || currentlySelected.Contains(die))
                continue;
            if (!diceButtons.ContainsKey(die))
                continue;

            CombatEvents.OnDieToggled?.Invoke(die);
            currentlySelected.Add(die);
            if (diceButtonViews.TryGetValue(die, out var view))
                view.SetSelected(true);
        }

        if (currentlySelected.Count > 0)
            PinTooltipToDie(currentlySelected[currentlySelected.Count - 1]);
        else
            HideDieTooltip();

        RefreshRollButtonsInteractable();
    }

    private void RefreshRollButtonsInteractable()
    {
        var canRoll = _combatState == CombatState.WaitingForRoll;
        if (rollButton != null)
            rollButton.interactable = canRoll && currentlySelected.Count > 0;
        if (rollAllButton != null)
            rollAllButton.interactable = canRoll && diceButtons.Count > 0;
        UpdateNoDiceSelectedIndicator();
    }

    private void RegisterTrayHover(Button btn, DieAssetSO die)
    {
        if (btn == null || die == null) return;
        var go = btn.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            hoveredTooltipDie = die;
            ShowDieTooltip(die);
        });
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            if (hoveredTooltipDie == die)
                hoveredTooltipDie = null;
            if (pinnedTooltipDie != null)
                ShowDieTooltip(pinnedTooltipDie);
            else
                HideDieTooltip();
        });
        et.triggers.Add(exit);
    }

    private void PinTooltipToDie(DieAssetSO die)
    {
        pinnedTooltipDie = die;
        ShowDieTooltip(die);
    }

    private void UpdateNoDiceSelectedIndicator()
    {
        if (noDiceSelectedIndicator == null)
            return;
        noDiceSelectedIndicator.SetActive(AreRollControlsVisible() && currentlySelected.Count == 0);
    }

    private void ShowDieTooltip(DieAssetSO die)
    {
        if (dieTooltipOverlay == null || die == null)
            return;

        tooltipShownForDie = die;
        dieTooltipOverlay.ShowDie(die, false);
    }

    private void HideDieTooltip()
    {
        tooltipShownForDie = null;
        if (dieTooltipOverlay != null)
            dieTooltipOverlay.Hide();
    }

    private bool ClickShouldKeepTooltipOpen()
    {
        if (dieTooltipOverlay != null && dieTooltipOverlay.IsPointerOverDieTooltipPanel())
            return true;

        if (EventSystem.current == null) return false;

        var pointer = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0) return false; // Clicked non-UI world area.

        var trayTransform = diceButtonContainer;
        for (var i = 0; i < hits.Count; i++)
        {
            var hitTransform = hits[i].gameObject != null ? hits[i].gameObject.transform : null;
            if (hitTransform == null) continue;
            if (trayTransform != null && hitTransform.IsChildOf(trayTransform)) return true;
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

    private void UpdateRollsUI(int remaining, int max)
    {
        rollsRemaining = remaining;
        maxRolls = max;
        if (rollButtonText != null) rollButtonText.text = $"Roll!\n{remaining}/{max}";
    }

    private void HandleStateChange(CombatState state)
    {
        _combatState = state;
        bool isWaiting = AreRollControlsVisible();

        RefreshRollControlsVisibility();

        if (!isWaiting)
        {
            pinnedTooltipDie = null;
            hoveredTooltipDie = null;
            HideDieTooltip();
        }
        if (isWaiting)
            RefreshRollButtonsInteractable();
        if (endTurnButton != null) { bool showEndTurn = isWaiting && rollsRemaining > 0; endTurnButton.gameObject.SetActive(showEndTurn); endTurnButton.interactable = showEndTurn; }
        UpdateNoDiceSelectedIndicator();
    }

    bool AreRollControlsVisible() => _combatState == CombatState.WaitingForRoll;

    void RefreshRollControlsVisibility()
    {
        var showRollControls = AreRollControlsVisible();

        if (rollButton != null)
            rollButton.gameObject.SetActive(showRollControls);
        if (rollAllButton != null)
            rollAllButton.gameObject.SetActive(showRollControls);

        if (trayCanvasGroup == null)
            return;

        trayCanvasGroup.gameObject.SetActive(showRollControls);
        if (!showRollControls)
            return;

        var trayInteractive = !_rerollSelectionActive;
        trayCanvasGroup.interactable = trayInteractive;
        trayCanvasGroup.blocksRaycasts = trayInteractive;
        trayCanvasGroup.alpha = 1f;
    }
}