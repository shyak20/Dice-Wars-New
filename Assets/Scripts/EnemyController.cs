using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class EnemyController : MonoBehaviour
{
    public EnemyTypeSO enemyData;

    [Header("UI References")]
    public TMP_Text nameText;
    public Slider healthSlider;
    public TMP_Text healthText;
    public TMP_Text intentText;
    public TMP_Text armorText;
    public GameObject armorIcon;

    [Header("Visual Effects (Sprite Overlay)")]
    public SpriteRenderer enemySprite;
    public GameObject hitEffectObject;
    public float hitEffectDuration = 0.5f;
    public float flashDuration = 0.15f;
    public Color flashColor = Color.white; // We still need color reference for initialization

    [Header("Juice (Camera Shake)")]
    [Range(0.01f, 0.5f)] public float damageShakeDuration = 0.1f;
    [Range(0.01f, 1f)] public float damageShakeMagnitude = 0.2f;

    // Shader property ID for performance
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");

    private int currentHealth;
    private int currentArmor;
    private int currentCycleIndex = 0;
    private EnemyActionSO currentIntent;

    private Coroutine flashRoutine;
    private Coroutine effectRoutine;
    private Material enemyMaterial;

    public StatusEffectManager StatusEffects { get; private set; }

    public int GetCurrentHealth() => currentHealth;
    public int GetCurrentArmor() => currentArmor;

    private void Awake()
    {
        StatusEffects = GetComponent<StatusEffectManager>();
        if (StatusEffects == null)
            Debug.LogError("EnemyController: Missing StatusEffectManager component!");
    }

    private void Start()
    {
        if (enemyData != null) Initialize(enemyData);
        if (hitEffectObject != null) hitEffectObject.SetActive(false);

        // Access the unique instance of the material for this sprite
        if (enemySprite != null)
        {
            enemyMaterial = enemySprite.material;
            // Initialize the material properties
            enemyMaterial.SetColor(FlashColorID, flashColor);
            enemyMaterial.SetFloat(FlashAmountID, 0f);
        }
    }

    public void Initialize(EnemyTypeSO data)
    {
        enemyData = data;
        currentHealth = data.maxHealth;
        currentArmor = data.startArmor;
        currentCycleIndex = 0;

        if (nameText != null) nameText.text = data.enemyName;
        UpdateUI();
        PrepareNextAction();
    }

    public void TakeDamage(int amount)
    {
        var damageRemaining = amount;
        var armorDamage = 0;

        if (currentArmor > 0)
        {
            if (currentArmor >= damageRemaining)
            {
                armorDamage = damageRemaining;
                currentArmor -= damageRemaining;
                damageRemaining = 0;
            }
            else
            {
                armorDamage = currentArmor;
                damageRemaining -= currentArmor;
                currentArmor = 0;
            }
        }

        var healthDamage = 0;
        if (damageRemaining > 0)
        {
            healthDamage = Mathf.Min(damageRemaining, currentHealth);
            currentHealth -= damageRemaining;
            currentHealth = Mathf.Max(0, currentHealth);
        }

        Debug.Log($"{enemyData.enemyName} hit for {amount} — Armor absorbed: {armorDamage}, HP damage: {healthDamage}");

        UpdateUI();
        TriggerHitJuice();

        if (currentHealth <= 0)
        {
            Debug.Log($"{enemyData.enemyName} defeated!");
        }
    }

    public void TakeTrueDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
        UpdateUI();
        TriggerHitJuice();

        if (currentHealth <= 0)
        {
            Debug.Log($"{enemyData.enemyName} defeated!");
        }
    }

    public void AddArmor(int amount)
    {
        currentArmor += amount;
        UpdateUI();
    }

    public void ResetArmor()
    {
        currentArmor = 0;
        UpdateUI();
    }

    private void TriggerHitJuice()
    {
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(damageShakeDuration, damageShakeMagnitude);

        if (hitEffectObject != null)
        {
            if (effectRoutine != null) StopCoroutine(effectRoutine);
            effectRoutine = StartCoroutine(HitEffectSequence());
        }

        if (enemyMaterial != null)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(SolidFlashSequence());
        }
    }

    private IEnumerator HitEffectSequence()
    {
        hitEffectObject.SetActive(true);
        yield return new WaitForSeconds(hitEffectDuration);
        hitEffectObject.SetActive(false);
    }

    /// <summary>
    /// Uses the new Shader property instead of SpriteRenderer.color
    /// </summary>
    private IEnumerator SolidFlashSequence()
    {
        // Set Flash to 100% (Solid Color)
        enemyMaterial.SetFloat(FlashAmountID, 1f);
        yield return new WaitForSeconds(flashDuration);
        // Set Flash to 0% (Normal Texture)
        enemyMaterial.SetFloat(FlashAmountID, 0f);
        flashRoutine = null;
    }

    // --- Rest of sequential logic below ---

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

    private void UpdateUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = enemyData.maxHealth;
            healthSlider.value = currentHealth;
        }
        if (healthText != null) healthText.text = $"{currentHealth} / {enemyData.maxHealth}";

        if (armorText != null)
        {
            armorText.text = currentArmor > 0 ? currentArmor.ToString() : "";
            if (armorIcon != null) armorIcon.SetActive(currentArmor > 0);
        }
    }

    public EnemyActionSO GetCurrentAction() => currentIntent;
}