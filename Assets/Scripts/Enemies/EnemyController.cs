using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UniRx;

public class EnemyController : MonoBehaviour
{
    public EnemyTypeSO enemyData;

    [Header("UI References")]
    public TMP_Text nameText;
    public Slider healthSlider;
    public TMP_Text healthText;
    public Slider armorSlider;       // The "Armor Bar" slider
    public TMP_Text intentText;
    public TMP_Text armorText;       // The text showing the actual armor amount
    public GameObject armorIcon;

    [Header("Floating damage numbers")]
    [Tooltip("World position for damage popups; defaults to enemy sprite or this transform.")]
    [SerializeField] private Transform damageNumberWorldAnchor;

    [Header("Visual Effects (Sprite Overlay)")]
    public SpriteRenderer enemySprite;
    public GameObject hitEffectObject;
    public float hitEffectDuration = 0.5f;
    public float flashDuration = 0.15f;
    public Color flashColor = Color.white;

    [Header("Juice (Camera Shake)")]
    [Range(0.01f, 0.5f)] public float damageShakeDuration = 0.1f;
    [Range(0.01f, 1f)] public float damageShakeMagnitude = 0.2f;

    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");

    private int currentHealth;
    private int currentArmor;
    private int currentCycleIndex = 0;
    public ReactiveProperty<EnemyActionSO> CurrentIntent = new();

    private Coroutine flashRoutine;
    private Coroutine effectRoutine;
    private Material enemyMaterial;

    public StatusEffectManager StatusEffects { get; private set; }

    public int GetCurrentHealth() => currentHealth;
    public int GetCurrentArmor() => currentArmor;

    /// <summary>World target for power orb FX flying from the player into this enemy.</summary>
    public Transform GetPowerOrbHitAnchor()
    {
        if (damageNumberWorldAnchor != null) return damageNumberWorldAnchor;
        if (enemySprite != null) return enemySprite.transform;
        return transform;
    }

    private void Awake()
    {
        StatusEffects = GetComponent<StatusEffectManager>();
        if (StatusEffects == null)
            Debug.LogError("EnemyController: Missing StatusEffectManager component!");
    }

    private void Start()
    {
        if (hitEffectObject != null) hitEffectObject.SetActive(false);

        if (enemySprite != null)
        {
            enemyMaterial = enemySprite.material;
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

        if (amount > 0)
            CombatEvents.OnEnemyDamageNumber?.Invoke(amount, GetDamageNumberWorldPosition(), this);
    }

    private Vector3 GetDamageNumberWorldPosition()
    {
        if (damageNumberWorldAnchor != null) return damageNumberWorldAnchor.position;
        if (enemySprite != null) return enemySprite.transform.position;
        return transform.position;
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

        if (amount > 0)
            CombatEvents.OnEnemyDamageNumber?.Invoke(amount, GetDamageNumberWorldPosition(), this);
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

    private IEnumerator SolidFlashSequence()
    {
        enemyMaterial.SetFloat(FlashAmountID, 1f);
        yield return new WaitForSeconds(flashDuration);
        enemyMaterial.SetFloat(FlashAmountID, 0f);
        flashRoutine = null;
    }

    public void PrepareNextAction()
    {
        if (enemyData.actionCycle.Count == 0) return;

        if (enemyData.isSequential)
        {
            CurrentIntent.Value = enemyData.actionCycle[currentCycleIndex];
            currentCycleIndex = (currentCycleIndex + 1) % enemyData.actionCycle.Count;
        }
        else
        {
            int randomIndex = UnityEngine.Random.Range(0, enemyData.actionCycle.Count);
            CurrentIntent.Value = enemyData.actionCycle[randomIndex];
        }
    }


    private void UpdateUI()
    {
        // 1. Core Health Slider
        if (healthSlider != null)
        {
            healthSlider.maxValue = enemyData.maxHealth;
            healthSlider.value = currentHealth;
        }

        bool hasArmor = currentArmor > 0;

        // 2. Armor Bar Slider (The blue bar)
        if (armorSlider != null)
        {
            armorSlider.gameObject.SetActive(hasArmor);
            if (hasArmor)
            {
                if (currentArmor > armorSlider.maxValue) armorSlider.maxValue = currentArmor;
                armorSlider.value = currentArmor;
            }
        }

        // 3. Health text (always shown; armor uses armor bar + armorText only)
        if (healthText != null)
        {
            healthText.gameObject.SetActive(true);
            if (enemyData != null)
                healthText.text = $"{currentHealth} / {enemyData.maxHealth}";
            else
                healthText.text = currentHealth.ToString();
        }

        // 4. Small Armor Icon/Amount Display
        if (armorText != null)
        {
            armorText.text = hasArmor ? currentArmor.ToString() : "";
        }
        if (armorIcon != null)
        {
            armorIcon.SetActive(hasArmor);
        }
    }

    public EnemyActionSO GetCurrentAction() => CurrentIntent.Value;
}