using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatus : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Set from PlayerDataSO.startingMaxHealth at combat init (or RunManager run vitality when active).")]
    public int maxHealth { get; private set; }
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

    [Header("Power orb (support flight)")]
    [Tooltip("World-space target when the turn has no enemy damage (armor/support): orb flies here instead of to the enemy. Prefer an empty above the HP bar in world space.")]
    [SerializeField] private Transform powerOrbSupportWorldAnchor;

    [Header("Enemy physical hit juice")]
    [Tooltip("Optional. Shakes camera, hit VFX, sprite flash when damage uses EnemyPhysicalAttack.")]
    [SerializeField] private PlayerPhysicalHitFeedback physicalHitFeedback;

    public StatusEffectManager StatusEffects { get; private set; }

    public int GetCurrentHealth() => currentHealth;

    public Vector3 GetDamageNumberWorldPosition()
    {
        if (damageNumberWorldAnchor != null)
            return damageNumberWorldAnchor.position;
        return transform.position;
    }

    /// <summary>Target for power orb when the player turn deals no immediate enemy damage (e.g. armor only).</summary>
    public Transform GetPowerOrbSupportAnchor()
    {
        if (powerOrbSupportWorldAnchor != null)
            return powerOrbSupportWorldAnchor;
        if (healthSlider != null)
            return healthSlider.transform;
        return damageNumberWorldAnchor != null ? damageNumberWorldAnchor : transform;
    }

    /// <summary>Applies persisted run HP after loading the combat scene (see <see cref="RunManager"/>).</summary>
    public void ApplyRunVitality(int hp, int maxHp)
    {
        maxHealth = Mathf.Max(1, maxHp);
        currentHealth = Mathf.Clamp(hp, 1, maxHealth);
        UpdateUI();
    }

    /// <summary>Sets max/current HP from <see cref="PlayerDataSO.startingMaxHealth"/> when not using persisted run vitality.</summary>
    public void ApplyStartingHealthFromPlayerData(PlayerDataSO data)
    {
        if (data == null)
        {
            Debug.LogError("PlayerStatus.ApplyStartingHealthFromPlayerData: PlayerDataSO is null.");
            return;
        }

        maxHealth = Mathf.Max(1, data.startingMaxHealth);
        currentHealth = maxHealth;
        UpdateUI();
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"<color=green>Player healed {amount} HP. Current: {currentHealth}</color>");
        UpdateUI();
    }

    /// <summary>
    /// Increases max HP and heals by the same amount (e.g. 10/20 and +3 → 13/23). Use for "Meditate" and similar.
    /// </summary>
    public void AddMaxHealthAndHeal(int amount)
    {
        if (amount <= 0) return;
        maxHealth = Mathf.Max(1, maxHealth + amount);
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"<color=green>Player +{amount} max HP (max {maxHealth}) and healed; current {currentHealth}</color>");
        UpdateUI();
    }

    /// <summary>Raises max HP only (no current-HP change). Prefer <see cref="AddMaxHealthAndHeal"/> for "gain max and heal" effects.</summary>
    public void AddMaxHP(int amount)
    {
        if (amount == 0) return;
        maxHealth = Mathf.Max(1, maxHealth + amount);
        Debug.Log($"<color=green>Player ADD MAX HP {amount} . Current max: {maxHealth}</color>");
        UpdateUI();
    }

    private void Awake()
    {
        StatusEffects = GetComponent<StatusEffectManager>();
        if (StatusEffects == null)
            Debug.LogError("PlayerStatus: Missing StatusEffectManager component!");

        if (physicalHitFeedback == null)
            physicalHitFeedback = GetComponentInChildren<PlayerPhysicalHitFeedback>(true);

        maxHealth = 1;
        currentHealth = 1;
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
    /// <param name="floatingDamageNumberWorldOverride">When set, used as the world anchor for <see cref="CombatEvents.OnPlayerDamageNumber"/> (e.g. enemy position for thorns popups).</param>
    public void TakeDamage(int damage, PlayerDamageSource source = PlayerDamageSource.Generic, Vector3? floatingDamageNumberWorldOverride = null)
    {
        var hpBefore = currentHealth;
        var armorBefore = currentArmor;

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

        if (source == PlayerDamageSource.EnemyPhysicalAttack)
        {
            var armorLost = armorBefore - currentArmor;
            if (armorLost > 0)
                CombatEvents.OnPlayerArmorLostToEnemyPhysicalAttack?.Invoke(armorLost);
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
            var w = floatingDamageNumberWorldOverride ?? GetDamageNumberWorldPosition();
            CombatEvents.OnPlayerDamageNumber?.Invoke(damage, w);
        }

        if (physicalHitFeedback != null && source == PlayerDamageSource.EnemyPhysicalAttack && damage > 0)
        {
            var hpLost = hpBefore - currentHealth;
            physicalHitFeedback.OnEnemyPhysicalHit(damage, hpLost, maxHealth);
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