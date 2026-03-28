using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatus : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 100;
    private int currentHealth;
    private int currentArmor = 0;

    [Header("UI References")]
    public Slider healthSlider;
    public TMP_Text healthText;
    public TMP_Text armorText;
    public GameObject armorIcon; // Optional: A shield icon that shows when armor > 0

    public int GetCurrentHealth() => currentHealth;

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"<color=green>Player healed {amount} HP. Current: {currentHealth}</color>");
        UpdateUI();
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }

    /// <summary>
    /// Adds armor gained from defense dice.
    /// </summary>
    public void AddArmor(int amount)
    {
        currentArmor += amount;
        UnityEngine.Debug.Log($"<color=blue>Player gained {amount} Armor. Total: {currentArmor}</color>");
        UpdateUI();
    }

    /// <summary>
    /// Deducts damage from armor first, then health.
    /// </summary>
    public void TakeDamage(int damage)
    {
        // 1. Armor absorbs damage first
        int damageRemaining = damage;

        if (currentArmor > 0)
        {
            if (currentArmor >= damageRemaining)
            {
                currentArmor -= damageRemaining;
                damageRemaining = 0;
            }
            else
            {
                damageRemaining -= currentArmor;
                currentArmor = 0;
            }
        }

        // 2. Remaining damage hits Health
        if (damageRemaining > 0)
        {
            currentHealth -= damageRemaining;
            currentHealth = Mathf.Max(0, currentHealth);
            UnityEngine.Debug.Log($"<color=red>Player took {damageRemaining} Health damage!</color>");
        }

        UpdateUI();

        if (currentHealth <= 0)
        {
            UnityEngine.Debug.LogError("Game Over: Player Health reached 0!");
            // Trigger Game Over Logic
        }
    }

    /// <summary>
    /// Usually called at the start of a player turn if you want armor to reset.
    /// </summary>
    public void ResetArmor()
    {
        currentArmor = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthText != null) healthText.text = $"{currentHealth} / {maxHealth}";

        if (armorText != null)
        {
            armorText.text = currentArmor > 0 ? currentArmor.ToString() : "";
            if (armorIcon != null) armorIcon.SetActive(currentArmor > 0);
        }
    }
}