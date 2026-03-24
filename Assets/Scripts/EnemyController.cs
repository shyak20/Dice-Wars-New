using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyController : MonoBehaviour
{
    public EnemyTypeSO enemyData;

    [Header("UI References")]
    public TMP_Text nameText;
    public Slider healthSlider;
    public TMP_Text healthText; // <--- The new text field
    public TMP_Text intentText;

    private int currentHealth;
    private int currentCycleIndex = 0;
    private EnemyActionSO currentIntent;

    private void Start()
    {
        if (enemyData != null) Initialize(enemyData);
    }

    public void Initialize(EnemyTypeSO data)
    {
        enemyData = data;
        currentHealth = data.maxHealth;
        currentCycleIndex = 0;

        if (nameText != null) nameText.text = data.enemyName;
        UpdateUI();
        PrepareNextAction();
    }

    public void PrepareNextAction()
    {
        if (enemyData.actionCycle.Count == 0) return;

        if (enemyData.isSequential)
        {
            currentIntent = enemyData.actionCycle[currentCycleIndex];
            currentCycleIndex = (currentCycleIndex + 1) % enemyData.actionCycle.Count;
        }
        else
        {
            int randomIndex = UnityEngine.Random.Range(0, enemyData.actionCycle.Count);
            currentIntent = enemyData.actionCycle[randomIndex];
        }

        UpdateIntentUI();
    }

    private void UpdateIntentUI()
    {
        if (intentText == null) return;

        string description = "";
        if (currentIntent.damage > 0)
        {
            description += (currentIntent.numberOfAttacks > 1)
                ? $"{currentIntent.damage}x{currentIntent.numberOfAttacks} ATK"
                : $"{currentIntent.damage} ATK";
        }

        if (currentIntent.armor > 0)
        {
            if (description != "") description += " & ";
            description += $"{currentIntent.armor} ARM";
        }

        intentText.text = $"Next: {currentIntent.actionName}\n({description})";
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
        UpdateUI();

        // Optional: Trigger a "Hit" animation here
    }

    private void UpdateUI()
    {
        // Update the bar
        if (healthSlider != null)
        {
            healthSlider.maxValue = enemyData.maxHealth;
            healthSlider.value = currentHealth;
        }

        // Update the numbers (e.g., "50 / 50")
        if (healthText != null)
        {
            healthText.text = $"{currentHealth} / {enemyData.maxHealth}";
        }
    }

    public EnemyActionSO GetCurrentAction() => currentIntent;
}