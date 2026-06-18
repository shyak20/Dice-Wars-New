using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fight feedback for an enemy: orb impact, physical vs burn indicator pulses, per-hit-type sprite flash tint, camera shake on damage,
/// and optional <see cref="Animator"/> driven by <see cref="EnemyTypeSO.combatAnimatorController"/> plus per-intent triggers on <see cref="EnemyActionSO"/>.
/// Physical indicator replaces the legacy hit-effect object. Wire the enemy's <see cref="SpriteRenderer"/> here.
/// </summary>
public sealed class EnemyCombatPresentationController : MonoBehaviour
{
    private static readonly List<EnemyCombatPresentationController> HoverOutlineRegistry = new List<EnemyCombatPresentationController>();

    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");

    [Header("Enemy sprite (shader flash)")]
    [SerializeField] private SpriteRenderer enemySprite;

    [Header("Drag assign hover")]
    [Tooltip("Applied to the enemy sprite while a rolled outcome token is dragged over this enemy. Falls back to RollTargetAssignmentController when unset.")]
    [SerializeField] private Material dragHoverOutlineMaterial;

    [Header("Enemy animator (optional)")]
    [Tooltip("Animator on the enemy presentation hierarchy. Runtime controller and idle come from EnemyTypeSO.")]
    [SerializeField] private Animator combatAnimator;
    [SerializeField] private Color orbSpriteFlashColor = new Color(1f, 0.95f, 0.6f);
    [SerializeField] private Color physicalSpriteFlashColor = Color.white;
    [SerializeField] private Color burnSpriteFlashColor = new Color(1f, 0.45f, 0.15f);
    [SerializeField, Min(0.01f)] private float flashDuration = 0.15f;

    [Header("Camera shake (damage)")]
    [SerializeField, Range(0.01f, 0.5f)] private float damageShakeDuration = 0.1f;
    [SerializeField, Range(0.01f, 1f)] private float damageShakeMagnitude = 0.2f;

    [Header("Indicators (usually inactive in hierarchy)")]
    [SerializeField] private GameObject orbImpactIndicator;
    [Tooltip("Replaces the old hit-effect object on physical damage.")]
    [SerializeField] private GameObject physicalDamageIndicator;
    [SerializeField] private GameObject burnDamageIndicator;
    [SerializeField, Min(0.02f)] private float orbImpactIndicatorSeconds = 0.12f;
    [SerializeField, Min(0.02f)] private float burnIndicatorSeconds = 0.12f;
    [SerializeField, Min(0.02f)] private float physicalDamageIndicatorSeconds = 0.5f;

    [Header("Death")]
    [Tooltip("Animator trigger fired when this enemy's HP reaches 0.")]
    [SerializeField] private string deathAnimatorTrigger = "Death";
    [Tooltip("Seconds after the death trigger before SplatterRevealSpritePlayer is added and dissolve begins.")]
    [SerializeField, Min(0f)] private float dissolveAttachDelaySeconds = 0.5f;
    [Tooltip("Seconds to animate splatter reveal from 1 (visible) to 0 (dissolved).")]
    [SerializeField, Min(0f)] private float dissolveDurationSeconds = 1.25f;
    [Tooltip("Applied to the enemy sprite when dissolve starts (DiceGame/UI Splatter Reveal URP).")]
    [SerializeField] private Material deathDissolveMaterial;
    [Tooltip("Seconds after defeat before this enemy GameObject is set inactive (add enemies). Main enemy stays visible until victory flow hides the enemy root.")]
    [SerializeField, Min(0f)] private float defeatedHideDelaySeconds = 2f;

    public float DefeatedHideDelaySeconds => defeatedHideDelaySeconds;

    private static readonly int RevealAmountId = Shader.PropertyToID("_RevealAmount");

    private EnemyController _enemy;
    private Material _defaultSharedMaterialSource;
    private Material _defaultMaterial;
    private Material _outlineMaterialRuntime;
    private Material _enemyMaterial;
    private bool _dragHoverOutlineActive;
    private Coroutine _spriteFlashRoutine;
    private Coroutine _orbRoutine;
    private Coroutine _physicalRoutine;
    private Coroutine _burnRoutine;
    private Coroutine _deathDissolveRoutine;

    /// <summary>Sprite used for hit flash and for fallbacks on <see cref="EnemyController"/> anchors.</summary>
    public SpriteRenderer EnemySprite => enemySprite;

    private void Awake()
    {
        _enemy = GetComponentInParent<EnemyController>();
        if (_enemy == null)
            throw new System.InvalidOperationException($"{nameof(EnemyCombatPresentationController)} on '{name}': must live under a GameObject with EnemyController.");

        if (enemySprite != null)
        {
            _defaultSharedMaterialSource = enemySprite.sharedMaterial;
            if (_defaultSharedMaterialSource == null)
            {
                Debug.LogError(
                    $"{nameof(EnemyCombatPresentationController)} on '{name}': assign a default material on {nameof(enemySprite)}.",
                    this);
            }
            else
                ApplyDefaultSpriteMaterial();
        }

        if (physicalDamageIndicator != null) physicalDamageIndicator.SetActive(false);
        if (burnDamageIndicator != null) burnDamageIndicator.SetActive(false);
        if (orbImpactIndicator != null) orbImpactIndicator.SetActive(false);
    }

    /// <summary>Restores the combat flash material and clears death-dissolve state when this slot is reused for a new enemy.</summary>
    public void ResetPresentationForSpawn()
    {
        if (_orbRoutine != null)
        {
            StopCoroutine(_orbRoutine);
            _orbRoutine = null;
        }

        if (_physicalRoutine != null)
        {
            StopCoroutine(_physicalRoutine);
            _physicalRoutine = null;
        }

        if (_burnRoutine != null)
        {
            StopCoroutine(_burnRoutine);
            _burnRoutine = null;
        }

        if (_spriteFlashRoutine != null)
        {
            StopCoroutine(_spriteFlashRoutine);
            _spriteFlashRoutine = null;
        }

        if (_deathDissolveRoutine != null)
        {
            StopCoroutine(_deathDissolveRoutine);
            _deathDissolveRoutine = null;
        }

        if (physicalDamageIndicator != null) physicalDamageIndicator.SetActive(false);
        if (burnDamageIndicator != null) burnDamageIndicator.SetActive(false);
        if (orbImpactIndicator != null) orbImpactIndicator.SetActive(false);

        SetDragAssignHoverOutline(false);
        StopDeathDissolve(clearComponent: true);
    }

    /// <summary>Stops indicator/flash coroutines and hides transient UI (e.g. new fight after returning from map).</summary>
    public void ResetTransientDamagePresentation()
    {
        ResetPresentationForSpawn();
    }

    void StopDeathDissolve(bool clearComponent)
    {
        if (_deathDissolveRoutine != null)
        {
            StopCoroutine(_deathDissolveRoutine);
            _deathDissolveRoutine = null;
        }

        if (enemySprite == null)
            return;

        var dissolve = enemySprite.GetComponent<SplatterRevealSpritePlayer>();
        if (dissolve != null)
        {
            dissolve.StopDissolve();
            dissolve.DisposeOwnedMaterial();
            if (clearComponent)
                Destroy(dissolve);
        }

        ApplyDefaultSpriteMaterial();
    }

    /// <summary>Restores the combat flash material on the enemy sprite (e.g. after active-turn material swap).</summary>
    public void RestoreCombatSpriteMaterial()
    {
        ApplyDefaultSpriteMaterial();
    }

    void ApplyDefaultSpriteMaterial()
    {
        if (enemySprite == null || _defaultSharedMaterialSource == null)
            return;

        if (_defaultMaterial != null && _defaultMaterial != _defaultSharedMaterialSource)
        {
            if (Application.isPlaying)
                Destroy(_defaultMaterial);
            else
                DestroyImmediate(_defaultMaterial);
        }

        _defaultMaterial = new Material(_defaultSharedMaterialSource);
        _defaultMaterial.shaderKeywords = _defaultSharedMaterialSource.shaderKeywords;
        enemySprite.SetPropertyBlock(null);
        enemySprite.sharedMaterial = _defaultMaterial;
        _enemyMaterial = _defaultMaterial;
        _enemyMaterial.SetColor(FlashColorID, physicalSpriteFlashColor);
        _enemyMaterial.SetFloat(FlashAmountID, 0f);
    }

    void RestoreSpriteMaterialAfterDissolve()
    {
        ApplyDefaultSpriteMaterial();
    }

    bool ValidateDeathDissolveMaterial()
    {
        if (deathDissolveMaterial == null)
        {
            Debug.LogError(
                $"{nameof(EnemyCombatPresentationController)} on '{name}': assign deathDissolveMaterial (UI Splatter Reveal URP).",
                this);
            return false;
        }

        if (!deathDissolveMaterial.HasProperty(RevealAmountId))
        {
            Debug.LogError(
                $"{nameof(EnemyCombatPresentationController)} on '{name}': deathDissolveMaterial must use shader with _RevealAmount.",
                this);
            return false;
        }

        return true;
    }

    void PrepareSpriteForDeathDissolve()
    {
        if (_spriteFlashRoutine != null)
        {
            StopCoroutine(_spriteFlashRoutine);
            _spriteFlashRoutine = null;
        }

        SetDragAssignHoverOutline(false);
    }

    /// <summary>Death trigger, then delayed runtime dissolve on the enemy sprite material.</summary>
    public void NotifyHealthDepleted()
    {
        TriggerDeathAnimator();
        StopDeathDissolve(clearComponent: true);
        _deathDissolveRoutine = StartCoroutine(CoAttachAndPlayDeathDissolve());
    }

    IEnumerator CoAttachAndPlayDeathDissolve()
    {
        if (dissolveAttachDelaySeconds > 0f)
            yield return new WaitForSeconds(dissolveAttachDelaySeconds);

        _deathDissolveRoutine = null;

        if (enemySprite == null)
        {
            Debug.LogError(
                $"{nameof(EnemyCombatPresentationController)} on '{name}': assign enemySprite for death dissolve.",
                this);
            yield break;
        }

        if (!ValidateDeathDissolveMaterial())
            yield break;

        PrepareSpriteForDeathDissolve();

        var spriteObject = enemySprite.gameObject;
        var dissolve = spriteObject.GetComponent<SplatterRevealSpritePlayer>();
        if (dissolve == null)
            dissolve = spriteObject.AddComponent<SplatterRevealSpritePlayer>();

        dissolve.PlayDissolveOut(enemySprite, dissolveDurationSeconds, deathDissolveMaterial);
        _enemyMaterial = enemySprite.material;
    }

    public void TriggerDeathAnimator()
    {
        if (combatAnimator == null || string.IsNullOrWhiteSpace(deathAnimatorTrigger))
            return;

        combatAnimator.SetTrigger(deathAnimatorTrigger);
    }

    /// <summary>Clears drag-hover outline on every enemy presentation (e.g. when a token drag ends without a drop).</summary>
    public static void ClearAllDragAssignHoverOutlines()
    {
        for (var i = HoverOutlineRegistry.Count - 1; i >= 0; i--)
        {
            var controller = HoverOutlineRegistry[i];
            if (controller != null)
                controller.SetDragAssignHoverOutline(false);
        }
    }

    public bool HasDragHoverOutlineConfigured => ResolveDragHoverOutlineMaterial() != null;

    public void SetDragAssignHoverOutline(bool active)
    {
        if (enemySprite == null || _defaultMaterial == null)
            return;

        if (active)
        {
            var outline = ResolveDragHoverOutlineMaterial();
            if (outline == null)
                return;

            if (_outlineMaterialRuntime == null || _outlineMaterialRuntime.shader != outline.shader)
                _outlineMaterialRuntime = new Material(outline);

            if (_dragHoverOutlineActive && enemySprite.material == _outlineMaterialRuntime)
                return;

            _dragHoverOutlineActive = true;
            enemySprite.material = _outlineMaterialRuntime;
            return;
        }

        if (!_dragHoverOutlineActive)
            return;

        _dragHoverOutlineActive = false;
        enemySprite.material = _defaultMaterial;
        _enemyMaterial = _defaultMaterial;
    }

    Material ResolveDragHoverOutlineMaterial()
    {
        if (dragHoverOutlineMaterial != null)
            return dragHoverOutlineMaterial;
        return RollTargetAssignmentController.SharedDragHoverOutlineMaterial;
    }

    /// <summary>Called from <see cref="EnemyController.Initialize"/> to apply art from <see cref="EnemyTypeSO"/>.</summary>
    public void ApplyDisplaySprite(Sprite sprite)
    {
        if (enemySprite == null || sprite == null)
            return;

        enemySprite.sprite = sprite;
        enemySprite.enabled = true;
        enemySprite.gameObject.SetActive(true);
    }

    /// <summary>Keeps the configured enemy sprite visible after roster activation (e.g. drop-target highlight wiring).</summary>
    public void EnsurePresentationVisible()
    {
        if (enemySprite == null)
            return;

        enemySprite.enabled = true;
        enemySprite.gameObject.SetActive(true);

        // Root SpriteRenderer on the animator object is unused when child enemySprite is wired.
        var rootSprite = GetComponent<SpriteRenderer>();
        if (rootSprite != null && rootSprite != enemySprite)
            rootSprite.enabled = false;
    }

    /// <summary>Assigns <see cref="EnemyTypeSO.combatAnimatorController"/> and rebinds the animator.</summary>
    public void SetupCombatAnimatorFromEnemyType(EnemyTypeSO data)
    {
        if (data == null) return;

        if (data.combatAnimatorController != null && combatAnimator == null)
        {
            Debug.LogError(
                $"{nameof(EnemyCombatPresentationController)} on '{name}': EnemyTypeSO '{data.name}' sets a combat animator controller but {nameof(combatAnimator)} is not assigned.",
                this);
            return;
        }

        if (combatAnimator == null)
            return;

        if (!combatAnimator.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                $"{nameof(EnemyCombatPresentationController)} on '{name}': cannot bind animator for '{data.name}' while inactive — activate the enemy before Initialize.",
                this);
            return;
        }

        combatAnimator.runtimeAnimatorController = data.combatAnimatorController;
        if (data.combatAnimatorController == null)
            return;

        combatAnimator.Rebind();
        combatAnimator.Update(0f);
        EnsurePresentationVisible();
    }

    /// <summary>Sets the intent's action trigger (if any) then waits <see cref="EnemyActionSO.actionAnimationLeadInSeconds"/>.</summary>
    public IEnumerator CoPresentEnemyTurnActionIntro(EnemyActionSO action)
    {
        if (action == null || combatAnimator == null || _enemy == null || _enemy.enemyData == null)
            yield break;
        if (_enemy.enemyData.combatAnimatorController == null)
            yield break;

        if (!string.IsNullOrWhiteSpace(action.actionAnimatorTriggerName))
            combatAnimator.SetTrigger(action.actionAnimatorTriggerName);

        if (action.actionAnimationLeadInSeconds > 0f)
            yield return new WaitForSeconds(action.actionAnimationLeadInSeconds);
    }

    /// <summary>No-op: action outro transitions are animator-driven.</summary>
    public IEnumerator CoPresentEnemyTurnActionOutro()
    {
        yield break;
    }

    private void OnEnable()
    {
        if (!HoverOutlineRegistry.Contains(this))
            HoverOutlineRegistry.Add(this);

        CombatEvents.OnPowerOrbImpact += OnOrbImpact;
        CombatEvents.OnEnemyDamagePresentation += OnEnemyDamage;
    }

    private void OnDisable()
    {
        SetDragAssignHoverOutline(false);
        HoverOutlineRegistry.Remove(this);

        CombatEvents.OnPowerOrbImpact -= OnOrbImpact;
        CombatEvents.OnEnemyDamagePresentation -= OnEnemyDamage;
    }

    private void OnOrbImpact(PowerOrbImpactPayload payload)
    {
        if (_enemy == null || payload.Target != PowerOrbImpactTarget.Enemy || payload.Enemy != _enemy) return;
        FlashSprite(orbSpriteFlashColor, withCameraShake: false);
        if (_orbRoutine != null) StopCoroutine(_orbRoutine);
        _orbRoutine = StartCoroutine(IndicatorPulseRoutine(orbImpactIndicator, orbImpactIndicatorSeconds, () => _orbRoutine = null));
    }

    private void OnEnemyDamage(int amount, Vector3 worldPosition, EnemyController damagedEnemy, EnemyDamagePresentationKind kind)
    {
        if (_enemy == null || damagedEnemy != _enemy || amount <= 0) return;

        var flashColor = kind == EnemyDamagePresentationKind.Burn ? burnSpriteFlashColor : physicalSpriteFlashColor;
        PlayDamageJuice(flashColor);

        if (kind == EnemyDamagePresentationKind.Burn)
        {
            if (_burnRoutine != null) StopCoroutine(_burnRoutine);
            _burnRoutine = StartCoroutine(IndicatorPulseRoutine(burnDamageIndicator, burnIndicatorSeconds, () => _burnRoutine = null));
        }
        else
        {
            if (_physicalRoutine != null) StopCoroutine(_physicalRoutine);
            _physicalRoutine = StartCoroutine(IndicatorPulseRoutine(physicalDamageIndicator, physicalDamageIndicatorSeconds, () => _physicalRoutine = null));
        }
    }

    private void PlayDamageJuice(Color spriteFlashTint)
    {
        FlashSprite(spriteFlashTint, withCameraShake: true);
    }

    private void FlashSprite(Color tint, bool withCameraShake)
    {
        if (withCameraShake)
            CameraShake.ShakeActive(damageShakeDuration, damageShakeMagnitude);

        if (_dragHoverOutlineActive || _enemyMaterial == null)
            return;
        if (_spriteFlashRoutine != null) StopCoroutine(_spriteFlashRoutine);
        _spriteFlashRoutine = StartCoroutine(SolidFlashSequence(tint));
    }

    private IEnumerator SolidFlashSequence(Color tint)
    {
        _enemyMaterial.SetColor(FlashColorID, tint);
        _enemyMaterial.SetFloat(FlashAmountID, 1f);
        yield return new WaitForSeconds(flashDuration);
        _enemyMaterial.SetFloat(FlashAmountID, 0f);
        _enemyMaterial.SetColor(FlashColorID, physicalSpriteFlashColor);
        _spriteFlashRoutine = null;
    }

    private IEnumerator IndicatorPulseRoutine(GameObject indicator, float seconds, System.Action onDone)
    {
        if (indicator == null)
        {
            onDone?.Invoke();
            yield break;
        }

        indicator.SetActive(true);
        yield return new WaitForSecondsRealtime(seconds);
        indicator.SetActive(false);
        onDone?.Invoke();
    }
}
