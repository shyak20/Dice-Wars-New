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

    [Header("Status Displays")]
    public Slider powerSlider;
    public TMP_Text powerText;
    public TMP_Text poolText;

    [Header("Bust Panel")]
    public GameObject bustPanel;
    public Button nullifyAttackButton;
    public Button nullifyDefenseButton;

    // We use the full name UnityEngine.UI.Image here to avoid the ambiguity error
    private Dictionary<DieAssetSO, UnityEngine.UI.Image> buttonImages = new Dictionary<DieAssetSO, UnityEngine.UI.Image>();
    private List<DieAssetSO> currentlySelected = new List<DieAssetSO>();

    private void OnEnable()
    {
        CombatEvents.OnPowerChanged += UpdatePowerUI;
        CombatEvents.OnPoolsUpdated += UpdatePoolsUI;
        CombatEvents.OnBustOccurred += ShowBustPanel;
        CombatEvents.OnStateChanged += HandleStateChange;
    }

    private void OnDisable()
    {
        CombatEvents.OnPowerChanged -= UpdatePowerUI;
        CombatEvents.OnPoolsUpdated -= UpdatePoolsUI;
        CombatEvents.OnBustOccurred -= ShowBustPanel;
        CombatEvents.OnStateChanged -= HandleStateChange;
    }

    private void Start()
    {
        if (bustPanel != null) bustPanel.SetActive(false);

        if (rollButton != null)
        {
            rollButton.onClick.AddListener(() => CombatEvents.OnRollCommand?.Invoke());
            rollButton.interactable = false;
        }

        Invoke(nameof(InitializeDiceButtons), 0.1f);
    }

    public void InitializeDiceButtons()
    {
        CombatManager manager = FindObjectOfType<CombatManager>();
        if (manager == null || manager.playerData == null) return;

        foreach (Transform child in diceButtonContainer) Destroy(child.gameObject);
        buttonImages.Clear();

        foreach (DieAssetSO die in manager.playerData.currentDeck)
        {
            GameObject prefab = (die.dieType == DieType.Attack) ? attackButtonPrefab : defenseButtonPrefab;
            GameObject btnObj = Instantiate(prefab, diceButtonContainer);

            TMP_Text txt = btnObj.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = die.dieName;

            // Explicitly getting the UI Image
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
            // Highlight selected (Soft Green)
            if (buttonImages.ContainsKey(die)) buttonImages[die].color = new Color(0.7f, 1f, 0.7f);
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

    private void UpdatePoolsUI(int atk, int def)
    {
        if (poolText != null) poolText.text = $"ATK: {atk} | DEF: {def}";
    }

    private void ShowBustPanel()
    {
        if (bustPanel == null) return;
        bustPanel.SetActive(true);

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

            if (state == CombatState.Rolling)
            {
                foreach (var img in buttonImages.Values) img.color = Color.white;
                currentlySelected.Clear();
                rollButton.interactable = false;
            }
        }
    }
}