using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyController : MonoBehaviour
{
    [Header("Data Connection")]
    public EnemyTypeSO enemyData;

    [Header("UI References")]
    public TMP_Text nameText;
    public Slider healthSlider;
    public TMP_Text healthText;
    public TMP_Text intentText; // Shows what the enemy will do next

    private int currentHealth;
    private EnemyActionSO currentIntent;

    private void Start()
    {
        if (enemyData != null) Initialize(enemyData);
    }

    public void Initialize(EnemyTypeSO data)
    {
        enemyData = data;
        currentHealth = data.maxHealth;

        if (nameText != null) nameText.text = data.enemyName;
        UpdateUI();
        PrepareNextAction();
    }

    /// <summary>
    /// Picks a random action from the list so the player can see it coming.
    /// </summary>
    public void PrepareNextAction()
    {
        if (enemyData.availableActions.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, enemyData.availableActions.Count);
            currentIntent = enemyData.availableActions[index];

            if (intentText != null)
                intentText.text = $"Intent: {currentIntent.actionName} ({currentIntent.damage} DMG)";
        }
    }

    public EnemyActionSO GetCurrentAction() => currentIntent;

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, enemyData.maxHealth);
        UpdateUI();

        if (currentHealth <= 0)
        {
            UnityEngine.Debug.Log($"{enemyData.enemyName} defeated!");
            // Logic for death (animations, etc.)
        }
    }

    private void UpdateUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = enemyData.maxHealth;
            healthSlider.value = currentHealth;
        }
        if (healthText != null) healthText.text = $"{currentHealth} / {enemyData.maxHealth}";
    }
}