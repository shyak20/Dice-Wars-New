using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;

public class EnemyController : MonoBehaviour
{
    public EnemyTypeSO enemyData;

    [Header("UI References")]
    public TMP_Text nameText;
    public Slider healthSlider;
    public TMP_Text healthText;
    public Slider armorSlider;       // The "Armor Bar" slider
    [Tooltip("Unused — enemy intent rows are built by EnemyActionUIController + EnemyIntentSegmentView.")]
    public TMP_Text intentText;
    public TMP_Text armorText;       // The text showing the actual armor amount
    public GameObject armorIcon;

    [Header("Floating damage numbers")]
    [Tooltip("World position for damage popups; defaults to enemy sprite or this transform.")]
    [SerializeField] private Transform damageNumberWorldAnchor;

    private EnemyCombatPresentationController _presentation;

    private int currentHealth;
    private int currentArmor;
    private int currentCycleIndex = 0;
    public ReactiveProperty<EnemyActionSO> CurrentIntent = new();

    public StatusEffectManager StatusEffects { get; private set; }

    public int GetCurrentHealth() => currentHealth;
    public int GetCurrentArmor() => currentArmor;

    /// <summary>World target for power orb FX flying from the player into this enemy.</summary>
    public Transform GetPowerOrbHitAnchor()
    {
        if (damageNumberWorldAnchor != null) return damageNumberWorldAnchor;
        var spr = _presentation != null ? _presentation.EnemySprite : null;
        if (spr != null) return spr.transform;
        return transform;
    }

    private void Awake()
    {
        StatusEffects = GetComponent<StatusEffectManager>();
        if (StatusEffects == null)
            Debug.LogError("EnemyController: Missing StatusEffectManager component!");

        _presentation = GetComponentInChildren<EnemyCombatPresentationController>(true);
    }

    public void Initialize(EnemyTypeSO data)
    {
        enemyData = data;
        currentHealth = data.maxHealth;
        currentArmor = data.startArmor;
        currentCycleIndex = 0;

        if (nameText != null) nameText.text = data.enemyName;

        var hasSprite = data.displaySprite != null;
        var hasAnimatorController = data.combatAnimatorController != null;
        if (!hasSprite && !hasAnimatorController)
            Debug.LogError(
                $"EnemyController on '{name}': EnemyTypeSO '{data.name}' should set {nameof(EnemyTypeSO.displaySprite)} and/or {nameof(EnemyTypeSO.combatAnimatorController)} for combat presentation.",
                this);

        if (_presentation == null)
        {
            if (hasSprite || hasAnimatorController)
                Debug.LogError(
                    $"EnemyController on '{name}': assign {nameof(EnemyCombatPresentationController)} in children when using display art or a combat animator.",
                    this);
        }
        else
        {
            if (hasSprite)
                _presentation.ApplyDisplaySprite(data.displaySprite);
            _presentation.SetupCombatAnimatorFromEnemyType(data);
        }

        UpdateUI();
        PrepareNextAction();
    }

    /// <summary>Wind-up before enemy damage/armor/game actions (see <see cref="EnemyActionSO.actionAnimationLeadInSeconds"/>).</summary>
    public IEnumerator CoPresentEnemyTurnActionIntro(EnemyActionSO action)
    {
        if (_presentation != null)
            yield return _presentation.CoPresentEnemyTurnActionIntro(action);
    }

    /// <summary>Return to idle after the intent's combat effects.</summary>
    public IEnumerator CoPresentEnemyTurnActionOutro()
    {
        if (_presentation != null)
            yield return _presentation.CoPresentEnemyTurnActionOutro();
    }

    public void TakeDamage(int amount, EnemyDamagePresentationKind presentationKind = EnemyDamagePresentationKind.Physical)
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

        if (currentHealth <= 0)
        {
            Debug.Log($"{enemyData.enemyName} defeated!");
        }

        if (amount > 0)
            CombatEvents.OnEnemyDamagePresentation?.Invoke(amount, GetDamageNumberWorldPosition(), this, presentationKind);
    }

    public Vector3 GetDamageNumberWorldPosition()
    {
        if (damageNumberWorldAnchor != null) return damageNumberWorldAnchor.position;
        var spr = _presentation != null ? _presentation.EnemySprite : null;
        if (spr != null) return spr.transform.position;
        return transform.position;
    }

    public void TakeTrueDamage(int amount, EnemyDamagePresentationKind presentationKind = EnemyDamagePresentationKind.Physical)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
        UpdateUI();

        if (currentHealth <= 0)
        {
            Debug.Log($"{enemyData.enemyName} defeated!");
        }

        if (amount > 0)
            CombatEvents.OnEnemyDamagePresentation?.Invoke(amount, GetDamageNumberWorldPosition(), this, presentationKind);
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

        // 2. Armor Bar Slider (The "Armor Bar" slider)
        if (armorSlider != null)
        {
            armorSlider.gameObject.SetActive(hasArmor);
            if (hasArmor)
            {
                if (currentArmor > armorSlider.maxValue) armorSlider.maxValue = currentArmor;
                armorSlider.value = currentArmor;
            }
        }

        if (healthText != null)
            healthText.text = currentHealth.ToString();

        // 4. Small Armor Icon/Amount Display
        if (armorText != null)
        {
            armorText.gameObject.SetActive(hasArmor);
            armorText.text = hasArmor ? currentArmor.ToString() : "";
        }
        if (armorIcon != null)
        {
            armorIcon.SetActive(hasArmor);
        }
    }

    public EnemyActionSO GetCurrentAction() => CurrentIntent.Value;
}
