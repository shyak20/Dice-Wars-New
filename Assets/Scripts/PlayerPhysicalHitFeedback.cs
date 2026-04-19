using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Juice when the player is hit by an enemy physical strike: optional VFX root, camera shake scaled by hit severity, sprite color flash.
/// </summary>
public sealed class PlayerPhysicalHitFeedback : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Defaults to a PlayerStatus on this object or a parent.")]
    [SerializeField] private PlayerStatus playerStatus;

    [Header("Hit VFX")]
    [Tooltip("Enabled while the hit plays; disabled after Hit Effect Duration.")]
    [SerializeField] private GameObject hitEffectRoot;
    [SerializeField, Min(0f)] private float hitEffectDuration = 0.35f;

    [Header("Camera shake (scaled by hit severity)")]
    [Tooltip("Severity = HP lost / max HP, or if armor absorbed all damage, incoming damage / max HP (each clamped 0–1).")]
    [SerializeField] private AnimationCurve damageRatioToShakeBlend = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float minShakeDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float maxShakeDuration = 0.32f;
    [SerializeField, Min(0f)] private float minShakeMagnitude = 0.02f;
    [SerializeField, Min(0f)] private float maxShakeMagnitude = 0.28f;

    [Header("Sprite hit flash")]
    [SerializeField] private Color hitColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField, Min(0.02f)] private float hitFlashDuration = 0.18f;
    [SerializeField] private List<SpriteRenderer> hitSpriteTargets = new List<SpriteRenderer>();

    private Coroutine _hitEffectRoutine;
    private Coroutine _flashRoutine;
    private Color[] _spriteBaseColors;
    private bool _cachedSpriteColors;

    private void Awake()
    {
        if (playerStatus == null)
            playerStatus = GetComponentInParent<PlayerStatus>();
        if (playerStatus == null)
            Debug.LogError("PlayerPhysicalHitFeedback: assign Player Status (or parent a PlayerStatus).", this);

        if (hitEffectRoot != null)
            hitEffectRoot.SetActive(false);

    }

    private void Reset()
    {
        damageRatioToShakeBlend = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    private void OnValidate()
    {
        if (maxShakeDuration < minShakeDuration)
            maxShakeDuration = minShakeDuration;
        if (maxShakeMagnitude < minShakeMagnitude)
            maxShakeMagnitude = minShakeMagnitude;
    }

    /// <summary>Call only for <see cref="PlayerDamageSource.EnemyPhysicalAttack"/> after armor and HP resolve.</summary>
    public void OnEnemyPhysicalHit(int grossDamage, int hpLost, int maxHp)
    {
        if (!isActiveAndEnabled || grossDamage <= 0)
            return;

        var max = Mathf.Max(1, maxHp);
        var ratio = hpLost > 0
            ? Mathf.Clamp01((float)hpLost / max)
            : Mathf.Clamp01((float)grossDamage / max);
        var blend = damageRatioToShakeBlend != null ? Mathf.Clamp01(damageRatioToShakeBlend.Evaluate(ratio)) : ratio;
        var shakeDur = Mathf.Lerp(minShakeDuration, maxShakeDuration, blend);
        var shakeMag = Mathf.Lerp(minShakeMagnitude, maxShakeMagnitude, blend);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(shakeDur, shakeMag);

        if (hitEffectRoot != null)
        {
            if (_hitEffectRoutine != null)
                StopCoroutine(_hitEffectRoutine);
            hitEffectRoot.SetActive(true);
            _hitEffectRoutine = StartCoroutine(CoTurnOffHitEffect());
        }

        if (hitSpriteTargets != null && hitSpriteTargets.Count > 0)
        {
            CacheSpriteColorsIfNeeded();
            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(CoFlashSprites());
        }
    }

    private IEnumerator CoTurnOffHitEffect()
    {
        yield return new WaitForSeconds(hitEffectDuration);
        if (hitEffectRoot != null)
            hitEffectRoot.SetActive(false);
        _hitEffectRoutine = null;
    }

    private void CacheSpriteColorsIfNeeded()
    {
        if (hitSpriteTargets == null || hitSpriteTargets.Count == 0)
            return;

        if (_spriteBaseColors != null && _spriteBaseColors.Length == hitSpriteTargets.Count && _cachedSpriteColors)
            return;

        _spriteBaseColors = new Color[hitSpriteTargets.Count];
        for (var i = 0; i < hitSpriteTargets.Count; i++)
        {
            var sr = hitSpriteTargets[i];
            if (sr == null)
                throw new System.InvalidOperationException($"PlayerPhysicalHitFeedback on '{name}': hit sprite target index {i} is null.");
            _spriteBaseColors[i] = sr.color;
        }

        _cachedSpriteColors = true;
    }

    private IEnumerator CoFlashSprites()
    {
        var half = hitFlashDuration * 0.5f;
        var t = 0f;
        while (t < half)
        {
            var u = half > 0.0001f ? t / half : 1f;
            ApplySpriteBlend(u);
            t += Time.deltaTime;
            yield return null;
        }

        ApplySpriteBlend(1f);
        t = 0f;
        while (t < half)
        {
            var u = half > 0.0001f ? t / half : 1f;
            ApplySpriteBlend(1f - u);
            t += Time.deltaTime;
            yield return null;
        }

        RestoreSpriteColors();
        _flashRoutine = null;
    }

    private void ApplySpriteBlend(float blend)
    {
        for (var i = 0; i < hitSpriteTargets.Count; i++)
        {
            var sr = hitSpriteTargets[i];
            if (sr == null || _spriteBaseColors == null || i >= _spriteBaseColors.Length)
                continue;
            sr.color = Color.Lerp(_spriteBaseColors[i], hitColor, blend);
        }
    }

    private void RestoreSpriteColors()
    {
        if (!_cachedSpriteColors || _spriteBaseColors == null)
            return;
        for (var i = 0; i < hitSpriteTargets.Count && i < _spriteBaseColors.Length; i++)
        {
            if (hitSpriteTargets[i] != null)
                hitSpriteTargets[i].color = _spriteBaseColors[i];
        }
    }

    private void OnDisable()
    {
        if (_hitEffectRoutine != null)
        {
            StopCoroutine(_hitEffectRoutine);
            _hitEffectRoutine = null;
        }

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        if (hitEffectRoot != null)
            hitEffectRoot.SetActive(false);
        RestoreSpriteColors();
    }
}
