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

    [Header("Floating damage numbers")]
    [Tooltip("World position used for damage popups; defaults to this transform.")]
    [SerializeField] private Transform damageNumberWorldAnchor;

    public StatusEffectManager StatusEffects { get; private set; }

    public int GetCurrentHealth() => currentHealth;

    /// <summary>Applies persisted run HP after loading the combat scene (see <see cref="RunManager"/>).</summary>
    public void ApplyRunVitality(int hp, int maxHp)
    {
        maxHealth = Mathf.Max(1, maxHp);
        currentHealth = Mathf.Clamp(hp, 1, maxHealth);
        UpdateUI();
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"<color=green>Player healed {amount} HP. Current: {currentHealth}</color>");
        UpdateUI();
    }
    
    public void AddMaxHP(int amount)
    {
        maxHealth += amount;
        Debug.Log($"<color=green>Player ADD MAX HP {amount} . Current: {maxHealth}</color>");
        UpdateUI();
    }

    private void Awake()
    {
        StatusEffects = GetComponent<StatusEffectManager>();
        if (StatusEffects == null)
            Debug.LogError("PlayerStatus: Missing StatusEffectManager component!");

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
            UnityEngine.Debug.Log($"<color=red>Player tasking armor reduction {currentArmor} of {damageRemaining}</color>");
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

        if (damage > 0)
        {
            Vector3 w = damageNumberWorldAnchor != null ? damageNumberWorldAnchor.position : transform.position;
            CombatEvents.OnPlayerDamageNumber?.Invoke(damage, w);
        }

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