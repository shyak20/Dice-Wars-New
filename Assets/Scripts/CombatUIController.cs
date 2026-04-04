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
    public GameObject attackButtonPrefab;
    public GameObject defenseButtonPrefab;

    [Header("Controls")]
    public Button rollButton;
    public Button endTurnButton;

    [Header("Status Displays")]
    public Slider powerSlider;
    public TMP_Text powerText;
    public TMP_Text poolText;

    [Header("Bust Panel")]
    public GameObject bustPanel;
    public Button nullifyAttackButton;
    public Button nullifyDefenseButton;

    private Dictionary<DieAssetSO, UnityEngine.UI.Image> buttonImages = new Dictionary<DieAssetSO, UnityEngine.UI.Image>();
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
            if (rollButtonText == null)
                Debug.LogError("CombatUIController: Roll button has no TMP_Text child!");
            rollButton.onClick.AddListener(() => CombatEvents.OnRollCommand?.Invoke());
            rollButton.interactable = false;
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(() => CombatEvents.OnEndTurnPressed?.Invoke());
        }

        Invoke(nameof(InitializeDiceButtons), 0.15f);
    }

    public void InitializeDiceButtons()
    {
        if (PlayerDataContainer.Instance == null || PlayerDataContainer.Instance.RuntimeData == null)
        {
            Debug.LogError("CombatUIController: PlayerDataContainer not found!");
            return;
        }

        foreach (Transform child in diceButtonContainer) Destroy(child.gameObject);
        buttonImages.Clear();
        currentlySelected.Clear();

        foreach (DieAssetSO die in PlayerDataContainer.Instance.RuntimeData.currentDeck)
        {
            GameObject prefab = (die.dieType == DieType.Shadow) ? attackButtonPrefab : defenseButtonPrefab;
            GameObject btnObj = Instantiate(prefab, diceButtonContainer);

            TMP_Text txt = btnObj.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = die.dieName;

            UnityEngine.UI.Image btnImg = btnObj.GetComponent<UnityEngine.UI.Image>();
            if (btnImg != null) buttonImages.Add(die, btnImg);

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
            if (buttonImages.ContainsKey(die)) buttonImages[die].color = Color.white;
        }
        else
        {
            currentlySelected.Add(die);
            if (buttonImages.ContainsKey(die)) buttonImages[die].color = new Color(0.6f, 1f, 0.9f);
        }

        rollButton.interactable = currentlySelected.Count > 0;
    }

    private void UpdatePowerUI(int current, int max)
    {
        if (powerSlider != null)
        {
            powerSlider.maxValue = max;
            powerSlider.value = current;
        }
        if (powerText != null) powerText.text = $"{current} / {max}";
    }

    private void UpdatePoolsUI(Dictionary<DieType, int> pools)
    {
        if (poolText != null)
            poolText.text = $"ATK: {pools[DieType.Shadow]} | DEF: {pools[DieType.Defense]}";
    }

    /// <summary>
    /// Logic: Only show buttons if their pool is > 0.
    /// </summary>
    private void ShowBustPanel(int currentAtk, int currentDef)
    {
        if (bustPanel == null) return;
        bustPanel.SetActive(true);

        // 1. Determine visibility
        nullifyAttackButton.gameObject.SetActive(currentAtk > 0);
        nullifyDefenseButton.gameObject.SetActive(currentDef > 0);

        // 2. Clear and set listeners
        nullifyAttackButton.onClick.RemoveAllListeners();
        nullifyDefenseButton.onClick.RemoveAllListeners();

        nullifyAttackButton.onClick.AddListener(() => {
            CombatEvents.OnBustResolved?.Invoke(true);
            bustPanel.SetActive(false);
        });

        nullifyDefenseButton.onClick.AddListener(() => {
            CombatEvents.OnBustResolved?.Invoke(false);
            bustPanel.SetActive(false);
        });
    }

    private void UpdateRollsUI(int remaining, int max)
    {
        rollsRemaining = remaining;
        maxRolls = max;

        if (rollButtonText != null)
            rollButtonText.text = $"Roll!\n{remaining}/{max}";
    }

    private void HandleStateChange(CombatState state)
    {
        bool isWaiting = (state == CombatState.WaitingForRoll);

        if (trayCanvasGroup != null)
        {
            trayCanvasGroup.interactable = isWaiting;
            trayCanvasGroup.alpha = isWaiting ? 1f : 0.5f;
        }

        if (rollButton != null)
        {
            rollButton.gameObject.SetActive(isWaiting);

            if (isWaiting)
            {
                // When we return to waiting, ensure the button 
                // reflects the persistent selection count.
                rollButton.interactable = currentlySelected.Count > 0;
            }
            else if (state == CombatState.Rolling)
            {
                // REMOVED the color reset and currentlySelected.Clear()
                // Just disable the button so they can't double-click while rolling.
                rollButton.interactable = false;
            }
        }

        if (endTurnButton != null)
        {
            bool showEndTurn = isWaiting && rollsRemaining > 0;
            endTurnButton.gameObject.SetActive(showEndTurn);
            endTurnButton.interactable = showEndTurn;
        }
    }
}