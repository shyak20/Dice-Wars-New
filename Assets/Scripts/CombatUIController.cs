using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class CombatUIController : MonoBehaviour
{
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

    private Dictionary<DieAssetSO, DiceTrayButtonView> diceButtonViews = new Dictionary<DieAssetSO, DiceTrayButtonView>();
    private List<DieAssetSO> currentlySelected = new List<DieAssetSO>();

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
        currentlySelected.Clear();

        foreach (DieAssetSO die in PlayerDataContainer.Instance.RuntimeData.currentDeck)
        {
            // Logic updated for renamed types
            GameObject prefab = (die.dieType == DieType.Damage) ? damageButtonPrefab : armorButtonPrefab;
            GameObject btnObj = Instantiate(prefab, diceButtonContainer);

            TMP_Text txt = btnObj.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = die.dieName;

            var trayView = btnObj.GetComponent<DiceTrayButtonView>();
            if (trayView != null)
                diceButtonViews.Add(die, trayView);
            else
                Debug.LogError($"CombatUIController: prefab '{prefab.name}' is missing DiceTrayButtonView. Add the component and assign regular/selected images.");

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => ToggleSelection(die));
        }
    }

    private void ToggleSelection(DieAssetSO die)
    {
        CombatEvents.OnDieToggled?.Invoke(die);
        if (currentlySelected.Contains(die))
        {
            currentlySelected.Remove(die);
            if (diceButtonViews.TryGetValue(die, out var view))
                view.SetSelected(false);
        }
        else
        {
            currentlySelected.Add(die);
            if (diceButtonViews.TryGetValue(die, out var view))
                view.SetSelected(true);
        }
        rollButton.interactable = currentlySelected.Count > 0;
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
        if (trayCanvasGroup != null) { trayCanvasGroup.interactable = isWaiting; trayCanvasGroup.alpha = isWaiting ? 1f : 0.5f; }
        if (rollButton != null) { rollButton.gameObject.SetActive(isWaiting); if (isWaiting) rollButton.interactable = currentlySelected.Count > 0; }
        if (endTurnButton != null) { bool showEndTurn = isWaiting && rollsRemaining > 0; endTurnButton.gameObject.SetActive(showEndTurn); endTurnButton.interactable = showEndTurn; }
    }
}