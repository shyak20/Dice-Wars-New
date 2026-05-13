using System.Collections;
using System.Collections.Generic;
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

    [Header("Canvas UI (optional)")]
    [Tooltip("Rect on a Canvas whose anchoredPosition.y follows the top of the enemy presentation sprite.")]
    [SerializeField] private RectTransform spriteTopFollowRect;
    [Tooltip("Added to screen Y after projecting the sprite top (typically a few pixels to sit above the art).")]
    [SerializeField] private float spriteTopFollowScreenYOffset;
    [Tooltip("Camera used to project the sprite into screen space. When null, uses Camera.main.")]
    [SerializeField] private Camera worldCameraForSpriteTopFollow;

    private EnemyCombatPresentationController _presentation;
    private Canvas _spriteTopFollowCanvas;
    private RectTransform _spriteTopFollowParentRect;
    private bool _loggedSpriteTopFollowCameraMissing;

    private int currentHealth;
    private int currentArmor;
    private int currentCycleIndex = 0;
    private int _currentPhaseIndex;
    private bool _pendingPhaseAdvance;
    private readonly Dictionary<EnemyResistanceElement, float> _damageResistanceByElement = new Dictionary<EnemyResistanceElement, float>();
    private readonly List<EnemyValueRolledListener> _valueRolledListeners = new List<EnemyValueRolledListener>();
    public ReactiveProperty<EnemyActionSO> CurrentIntent = new();

    public StatusEffectManager StatusEffects { get; private set; }
    public IReadOnlyDictionary<EnemyResistanceElement, float> DamageResistances => _damageResistanceByElement;
    public IReadOnlyList<EnemyValueRolledListener> ValueRolledListeners => _valueRolledListeners;

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

    private void OnEnable()
    {
        CacheSpriteTopFollowUi();
    }

    private void LateUpdate()
    {
        UpdateSpriteTopFollowRectY();
    }

    public void Initialize(EnemyTypeSO data)
    {
        enemyData = data;
        currentHealth = data.maxHealth;
        currentArmor = data.startArmor;
        currentCycleIndex = 0;
        _currentPhaseIndex = 0;
        _pendingPhaseAdvance = false;
        ConfigureStartingBuffs();

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

    public int ApplyElementResistance(int amount, DieType damageType)
    {
        if (amount <= 0)
            return 0;

        var resistanceElement = ToResistanceElement(damageType);
        if (!_damageResistanceByElement.TryGetValue(resistanceElement, out var lessPercent) || lessPercent <= 0f)
            return amount;

        var multiplier = Mathf.Clamp01(1f - (lessPercent / 100f));
        return Mathf.Max(0, Mathf.RoundToInt(amount * multiplier));
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
        var inferredType = presentationKind == EnemyDamagePresentationKind.Burn ? DieType.Fire : DieType.Damage;
        TakeDamage(amount, inferredType, presentationKind);
    }

    public void TakeDamage(int amount, DieType damageType, EnemyDamagePresentationKind presentationKind = EnemyDamagePresentationKind.Physical)
    {
        if (amount <= 0) return;

        if (presentationKind == EnemyDamagePresentationKind.Burn && StatusEffects != null)
        {
            var burnCtx = StatusEffects.CreateContextForEnemy(this);
            amount = StatusEffects.ApplyBurnDamageModifiers(burnCtx, amount);
            if (amount <= 0) return;
        }

        amount = ApplyElementResistance(amount, damageType);
        if (amount <= 0) return;

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
        else
            EvaluatePendingPhaseTransitionTrigger();

        if (amount > 0)
            CombatEvents.OnEnemyDamagePresentation?.Invoke(amount, GetDamageNumberWorldPosition(), this, presentationKind);
    }

    public void HandlePlayerFaceResolved(FaceResult resolvedFace, CombatManager combatManager)
    {
        if (resolvedFace == null || combatManager == null || _valueRolledListeners.Count == 0)
            return;

        for (var i = 0; i < _valueRolledListeners.Count; i++)
        {
            var listener = _valueRolledListeners[i];
            if (listener == null || listener.rolledValues == null || listener.actions == null)
                continue;
            if (!listener.rolledValues.Contains(resolvedFace.Value))
                continue;

            var ctx = combatManager.BuildEnemyPassiveActionContext(resolvedFace);
            for (var a = 0; a < listener.actions.Count; a++)
            {
                var action = listener.actions[a];
                if (action == null || action is FaceResolveModifierBase)
                    continue;
                action.Execute(ctx);
            }
        }
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
        else
            EvaluatePendingPhaseTransitionTrigger();

        if (amount > 0)
            CombatEvents.OnEnemyDamagePresentation?.Invoke(amount, GetDamageNumberWorldPosition(), this, presentationKind);
    }

    public void AddArmor(int amount)
    {
        currentArmor += amount;
        UpdateUI();
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || enemyData == null)
            return;
        currentHealth = Mathf.Min(currentHealth + amount, enemyData.maxHealth);
        UpdateUI();
    }

    public void ResetArmor()
    {
        currentArmor = 0;
        UpdateUI();
    }

    public void PrepareNextAction()
    {
        ApplyPendingPhaseAdvanceIfAny();
        var activeCycle = GetActiveActionCycle();
        if (activeCycle == null || activeCycle.Count == 0) return;

        if (enemyData.isSequential)
        {
            CurrentIntent.Value = activeCycle[currentCycleIndex];
            currentCycleIndex = (currentCycleIndex + 1) % activeCycle.Count;
        }
        else
        {
            int randomIndex = UnityEngine.Random.Range(0, activeCycle.Count);
            CurrentIntent.Value = activeCycle[randomIndex];
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

    private List<EnemyActionSO> GetActiveActionCycle()
    {
        if (enemyData == null) return null;
        if (enemyData.HasConfiguredPhases)
        {
            var phases = enemyData.Phases;
            var idx = Mathf.Clamp(_currentPhaseIndex, 0, phases.Count - 1);
            return phases[idx].actionCycle;
        }

        return enemyData.actionCycle;
    }

    private void EvaluatePendingPhaseTransitionTrigger()
    {
        if (enemyData == null || !enemyData.HasConfiguredPhases || _pendingPhaseAdvance)
            return;

        var phases = enemyData.Phases;
        var nextPhaseIndex = _currentPhaseIndex + 1;
        if (nextPhaseIndex >= phases.Count)
            return;

        var next = phases[nextPhaseIndex];
        if (next == null || next.actionCycle == null || next.actionCycle.Count == 0)
            return;

        if (currentHealth <= next.phaseTargetHealth)
            _pendingPhaseAdvance = true;
    }

    private void ApplyPendingPhaseAdvanceIfAny()
    {
        if (!_pendingPhaseAdvance || enemyData == null || !enemyData.HasConfiguredPhases)
            return;

        var phases = enemyData.Phases;
        var nextPhaseIndex = _currentPhaseIndex + 1;
        if (nextPhaseIndex >= phases.Count)
        {
            _pendingPhaseAdvance = false;
            return;
        }

        var next = phases[nextPhaseIndex];
        if (next == null || next.actionCycle == null || next.actionCycle.Count == 0)
        {
            _pendingPhaseAdvance = false;
            return;
        }

        _currentPhaseIndex = nextPhaseIndex;
        currentCycleIndex = 0;
        _pendingPhaseAdvance = false;
    }

    private void ConfigureStartingBuffs()
    {
        _damageResistanceByElement.Clear();
        _valueRolledListeners.Clear();

        if (enemyData == null)
            return;

        if (enemyData.startingResistances != null)
        {
            for (var i = 0; i < enemyData.startingResistances.Count; i++)
            {
                var entry = enemyData.startingResistances[i];
                if (entry == null || entry.lessDamagePercent <= 0f)
                    continue;

                var current = 0f;
                _damageResistanceByElement.TryGetValue(entry.element, out current);
                _damageResistanceByElement[entry.element] = Mathf.Clamp(current + entry.lessDamagePercent, 0f, 100f);
            }
        }

        if (enemyData.valueRolledListeners != null)
        {
            for (var i = 0; i < enemyData.valueRolledListeners.Count; i++)
            {
                var listener = enemyData.valueRolledListeners[i];
                if (listener == null || listener.rolledValues == null || listener.rolledValues.Count == 0 || listener.actions == null || listener.actions.Count == 0)
                    continue;
                _valueRolledListeners.Add(listener);
            }
        }
    }

    private static EnemyResistanceElement ToResistanceElement(DieType damageType)
    {
        switch (damageType)
        {
            case DieType.Fire:
                return EnemyResistanceElement.Fire;
            case DieType.Ice:
                return EnemyResistanceElement.Ice;
            case DieType.Nature:
                return EnemyResistanceElement.Nature;
            default:
                return EnemyResistanceElement.Physical;
        }
    }

    private void CacheSpriteTopFollowUi()
    {
        _spriteTopFollowCanvas = null;
        _spriteTopFollowParentRect = null;
        if (spriteTopFollowRect == null)
            return;

        _spriteTopFollowCanvas = spriteTopFollowRect.GetComponentInParent<Canvas>();
        _spriteTopFollowParentRect = spriteTopFollowRect.parent as RectTransform;

        if (_spriteTopFollowCanvas == null)
            Debug.LogError(
                $"EnemyController on '{name}': {nameof(spriteTopFollowRect)} must sit under a Canvas.",
                this);

        if (_spriteTopFollowParentRect == null)
            Debug.LogError(
                $"EnemyController on '{name}': {nameof(spriteTopFollowRect)} must have a RectTransform parent.",
                this);
    }

    private void UpdateSpriteTopFollowRectY()
    {
        if (spriteTopFollowRect == null || _spriteTopFollowParentRect == null || _spriteTopFollowCanvas == null)
            return;

        var sprite = _presentation != null ? _presentation.EnemySprite : null;
        if (sprite == null || !sprite.enabled)
            return;

        var bounds = sprite.bounds;
        var worldTop = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

        var cam = worldCameraForSpriteTopFollow != null ? worldCameraForSpriteTopFollow : Camera.main;
        if (cam == null)
        {
            if (!_loggedSpriteTopFollowCameraMissing)
            {
                _loggedSpriteTopFollowCameraMissing = true;
                Debug.LogError(
                    $"EnemyController on '{name}': assign {nameof(worldCameraForSpriteTopFollow)} or tag a Main Camera to drive {nameof(spriteTopFollowRect)}.",
                    this);
            }
            return;
        }

        var screen = cam.WorldToScreenPoint(worldTop);
        if (screen.z <= 0f)
            return;

        screen.y += spriteTopFollowScreenYOffset;

        var eventCam = _spriteTopFollowCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (_spriteTopFollowCanvas.worldCamera != null ? _spriteTopFollowCanvas.worldCamera : cam);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _spriteTopFollowParentRect,
                new Vector2(screen.x, screen.y),
                eventCam,
                out var localInParent))
            return;

        var ap = spriteTopFollowRect.anchoredPosition;
        ap.y = localInParent.y;
        spriteTopFollowRect.anchoredPosition = ap;
    }
}
