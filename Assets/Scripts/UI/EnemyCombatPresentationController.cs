using System.Collections;
using UnityEngine;

/// <summary>
/// Fight feedback for an enemy: orb impact, physical vs burn indicator pulses, per-hit-type sprite flash tint, and camera shake on damage.
/// Physical indicator replaces the legacy hit-effect object. Wire the enemy's <see cref="SpriteRenderer"/> here.
/// </summary>
public sealed class EnemyCombatPresentationController : MonoBehaviour
{
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");

    [Header("Enemy sprite (shader flash)")]
    [SerializeField] private SpriteRenderer enemySprite;
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

    private EnemyController _enemy;
    private Material _enemyMaterial;
    private Coroutine _spriteFlashRoutine;
    private Coroutine _orbRoutine;
    private Coroutine _physicalRoutine;
    private Coroutine _burnRoutine;

    /// <summary>Sprite used for hit flash and for fallbacks on <see cref="EnemyController"/> anchors.</summary>
    public SpriteRenderer EnemySprite => enemySprite;

    private void Awake()
    {
        _enemy = GetComponentInParent<EnemyController>();
        if (_enemy == null)
            throw new System.InvalidOperationException($"{nameof(EnemyCombatPresentationController)} on '{name}': must live under a GameObject with EnemyController.");

        if (enemySprite != null)
        {
            _enemyMaterial = enemySprite.material;
            _enemyMaterial.SetColor(FlashColorID, physicalSpriteFlashColor);
            _enemyMaterial.SetFloat(FlashAmountID, 0f);
        }

        if (physicalDamageIndicator != null) physicalDamageIndicator.SetActive(false);
        if (burnDamageIndicator != null) burnDamageIndicator.SetActive(false);
        if (orbImpactIndicator != null) orbImpactIndicator.SetActive(false);
    }

    /// <summary>Called from <see cref="EnemyController.Initialize"/> to apply art from <see cref="EnemyTypeSO"/>.</summary>
    public void ApplyDisplaySprite(Sprite sprite)
    {
        if (enemySprite != null && sprite != null)
            enemySprite.sprite = sprite;
    }

    private void OnEnable()
    {
        CombatEvents.OnPowerOrbImpact += OnOrbImpact;
        CombatEvents.OnEnemyDamagePresentation += OnEnemyDamage;
    }

    private void OnDisable()
    {
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
        if (withCameraShake && CameraShake.Instance != null)
            CameraShake.Instance.Shake(damageShakeDuration, damageShakeMagnitude);

        if (_enemyMaterial == null) return;
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
