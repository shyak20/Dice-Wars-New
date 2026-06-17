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

    [Header("Multi-enemy targeting")]
    [Tooltip("Optional. This enemy's own status-effect bar. When assigned, CombatManager binds debuffs to it (required for adds beyond the first enemy).")]
    [SerializeField] private StatusEffectBarUI ownStatusBar;
    [Tooltip("Optional. Per-enemy element layout where assigned damage/debuff outcomes accumulate under this enemy. Required for multi-enemy targeting.")]
    [SerializeField] private StoredActionsPoolDisplay assignedElementPool;
    [Tooltip("Optional. Drop target component used so rolled outcomes can be dragged onto this enemy. Required for multi-enemy targeting.")]
    [SerializeField] private EnemyDropTarget dropTarget;

    private EnemyCombatPresentationController _presentation;

    /// <summary>True while this enemy is part of the active combat roster (a configured, living participant).</summary>
    public bool IsActiveInRoster { get; private set; }

    /// <summary>This enemy's own status bar (may be null for the first enemy, which uses the shared CombatManager bar).</summary>
    public StatusEffectBarUI OwnStatusBar => ownStatusBar;

    /// <summary>Per-enemy element layout for assigned outcomes (may be null when targeting UI is not wired).</summary>
    public StoredActionsPoolDisplay AssignedElementPool => assignedElementPool;

    /// <summary>Drop target used for drag-to-assign (may be null when targeting UI is not wired).</summary>
    public EnemyDropTarget DropTarget => dropTarget;

    public EnemyCombatPresentationController CombatPresentation => _presentation;

    public bool IsAlive => currentHealth > 0;

    private int currentHealth;
    private int currentArmor;
    private int currentCycleIndex = 0;
    private int _currentPhaseIndex;
    private bool _pendingPhaseAdvance;
    private readonly Dictionary<EnemyResistanceElement, float> _damageResistanceByElement = new Dictionary<EnemyResistanceElement, float>();
    private readonly List<EnemyValueRolledListener> _valueRolledListeners = new List<EnemyValueRolledListener>();
    public ReactiveProperty<EnemyActionSO> CurrentIntent = new();

    Coroutine _hideAfterDefeatRoutine;

    /// <summary>True while waiting for <see cref="ScheduleDeactivateFromRoster"/> before <see cref="GameObject.SetActive"/> false.</summary>
    public bool IsHideAfterDefeatPending { get; private set; }

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

    public void Initialize(EnemyTypeSO data)
    {
        CancelScheduledHideAfterDefeat();
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
            _presentation.ResetPresentationForSpawn();
            if (hasSprite)
                _presentation.ApplyDisplaySprite(data.displaySprite);
            _presentation.SetupCombatAnimatorFromEnemyType(data);
        }

        UpdateUI();
        PrepareNextAction();
    }

    /// <summary>Marks this enemy as a live roster participant, enables its GameObject + targeting UI, and wires the drop target.</summary>
    public void ActivateInRoster(CombatManager combat)
    {
        CancelScheduledHideAfterDefeat();
        IsActiveInRoster = true;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (dropTarget != null)
            dropTarget.Bind(this, combat);

        if (_presentation != null)
            _presentation.EnsurePresentationVisible();

        if (assignedElementPool != null)
            assignedElementPool.gameObject.SetActive(true);
    }

    /// <summary>Removes this enemy from the active roster after defeat: clears its pending element pool and disables its GameObject.</summary>
    public void DeactivateFromRoster()
    {
        CancelScheduledHideAfterDefeat();
        MarkDefeatedInRoster();
        gameObject.SetActive(false);
    }

    /// <summary>Marks defeated and hides the GameObject after <paramref name="delaySeconds"/> (0 = immediate).</summary>
    public void ScheduleDeactivateFromRoster(float delaySeconds)
    {
        MarkDefeatedInRoster();

        CancelScheduledHideAfterDefeat();
        if (delaySeconds <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        IsHideAfterDefeatPending = true;
        _hideAfterDefeatRoutine = StartCoroutine(CoDeactivateAfterDefeat(delaySeconds));
    }

    void MarkDefeatedInRoster()
    {
        IsActiveInRoster = false;
        if (assignedElementPool != null)
            assignedElementPool.ClearAllRows();
    }

    void CancelScheduledHideAfterDefeat()
    {
        IsHideAfterDefeatPending = false;
        if (_hideAfterDefeatRoutine == null)
            return;

        StopCoroutine(_hideAfterDefeatRoutine);
        _hideAfterDefeatRoutine = null;
    }

    IEnumerator CoDeactivateAfterDefeat(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        IsHideAfterDefeatPending = false;
        _hideAfterDefeatRoutine = null;
        gameObject.SetActive(false);
    }

    /// <summary>Main Enemy defeat: clears the assigned element pool but keeps the GameObject (rewards reference its data).</summary>
    public void ClearAssignedPoolOnDefeat()
    {
        MarkDefeatedInRoster();
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

    /// <summary>Optional outro hook after intent effects (animator transitions are configured in Animator).</summary>
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
            NotifyPresentationHealthDepleted();
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
            NotifyPresentationHealthDepleted();
        }
        else
            EvaluatePendingPhaseTransitionTrigger();

        if (amount > 0)
            CombatEvents.OnEnemyDamagePresentation?.Invoke(amount, GetDamageNumberWorldPosition(), this, presentationKind);
    }

    void NotifyPresentationHealthDepleted()
    {
        if (_presentation != null)
            _presentation.NotifyHealthDepleted();
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
}
