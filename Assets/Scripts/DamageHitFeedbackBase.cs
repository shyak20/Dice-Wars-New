using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Camera shake + optional hit VFX / flash objects when the player takes damage.
/// Subclasses or callers invoke <see cref="PlayHit"/> with resolved damage values.
/// </summary>
public abstract class DamageHitFeedbackBase : MonoBehaviour
{
    [Header("Hit VFX")]
    [Tooltip("Enabled while the hit plays; disabled after Hit Effect Duration.")]
    [SerializeField] private GameObject hitEffectRoot;
    [SerializeField, Min(0f)] private float hitEffectDuration = 0.35f;

    [Header("Hit flash")]
    [Tooltip("Enabled on damage, disabled after Hit Flash Duration.")]
    [SerializeField] protected List<GameObject> hitFlashRoots = new List<GameObject>();
    [SerializeField, Min(0.02f)] private float hitFlashDuration = 0.18f;

    [Header("Poison hit VFX")]
    [Tooltip("Optional. Same behavior as Hit Effect Root, used when the player takes poison (true) damage.")]
    [SerializeField] private GameObject poisonHitEffectRoot;

    [Header("Poison hit flash")]
    [Tooltip("Optional. Same behavior as Hit Flash roots, used when the player takes poison (true) damage.")]
    [SerializeField] protected List<GameObject> poisonHitFlashRoots = new List<GameObject>();

    [Header("Camera shake (scaled by hit severity)")]
    [Tooltip("Severity = HP lost / max HP, or if armor absorbed all damage, incoming damage / max HP (each clamped 0–1).")]
    [SerializeField] private AnimationCurve damageRatioToShakeBlend = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float minShakeDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float maxShakeDuration = 0.32f;
    [SerializeField, Min(0f)] private float minShakeMagnitude = 0.02f;
    [SerializeField, Min(0f)] private float maxShakeMagnitude = 0.28f;

    private Coroutine _hitEffectRoutine;
    private Coroutine _flashRoutine;
    private Coroutine _poisonHitEffectRoutine;
    private Coroutine _poisonFlashRoutine;

    protected virtual void Awake()
    {
        if (hitEffectRoot != null)
            hitEffectRoot.SetActive(false);
        if (poisonHitEffectRoot != null)
            poisonHitEffectRoot.SetActive(false);
        if (HasHitFlashTargets(hitFlashRoots))
            SetHitFlashActive(hitFlashRoots, false);
        if (HasHitFlashTargets(poisonHitFlashRoots))
            SetHitFlashActive(poisonHitFlashRoots, false);
    }

    private void Reset()
    {
        damageRatioToShakeBlend = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxShakeDuration < minShakeDuration)
            maxShakeDuration = minShakeDuration;
        if (maxShakeMagnitude < minShakeMagnitude)
            maxShakeMagnitude = minShakeMagnitude;
    }
#endif

    protected void PlayHit(int grossDamage, int hpLost, int maxHp) =>
        PlayHitInternal(
            grossDamage,
            hpLost,
            maxHp,
            hitEffectRoot,
            hitEffectDuration,
            hitFlashRoots,
            hitFlashDuration,
            () => _hitEffectRoutine,
            routine => _hitEffectRoutine = routine,
            () => _flashRoutine,
            routine => _flashRoutine = routine);

    protected void PlayPoisonHit(int grossDamage, int hpLost, int maxHp) =>
        PlayHitInternal(
            grossDamage,
            hpLost,
            maxHp,
            poisonHitEffectRoot,
            hitEffectDuration,
            poisonHitFlashRoots,
            hitFlashDuration,
            () => _poisonHitEffectRoutine,
            routine => _poisonHitEffectRoutine = routine,
            () => _poisonFlashRoutine,
            routine => _poisonFlashRoutine = routine);

    void PlayHitInternal(
        int grossDamage,
        int hpLost,
        int maxHp,
        GameObject effectRoot,
        float effectDuration,
        List<GameObject> flashRoots,
        float flashDuration,
        Func<Coroutine> getEffectRoutine,
        Action<Coroutine> setEffectRoutine,
        Func<Coroutine> getFlashRoutine,
        Action<Coroutine> setFlashRoutine)
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

        CameraShake.ShakeActive(shakeDur, shakeMag);

        if (effectRoot != null)
        {
            var effectRoutine = getEffectRoutine();
            if (effectRoutine != null)
                StopCoroutine(effectRoutine);
            effectRoot.SetActive(true);
            setEffectRoutine(StartCoroutine(CoTurnOffAfterDuration(effectRoot, effectDuration, () => setEffectRoutine(null))));
        }

        if (!HasHitFlashTargets(flashRoots))
            return;

        var flashRoutine = getFlashRoutine();
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        SetHitFlashActive(flashRoots, true);
        setFlashRoutine(StartCoroutine(CoTurnOffHitFlash(flashRoots, flashDuration, () => setFlashRoutine(null))));
    }

    static bool HasHitFlashTargets(List<GameObject> roots)
    {
        if (roots == null || roots.Count == 0)
            return false;

        for (var i = 0; i < roots.Count; i++)
        {
            if (roots[i] != null)
                return true;
        }

        return false;
    }

    static void SetHitFlashActive(List<GameObject> roots, bool active)
    {
        if (roots == null)
            return;

        for (var i = 0; i < roots.Count; i++)
        {
            var root = roots[i];
            if (root == null)
                throw new InvalidOperationException($"DamageHitFeedbackBase: hit flash root index {i} is null.");
            root.SetActive(active);
        }
    }

    static IEnumerator CoTurnOffHitFlash(List<GameObject> flashRoots, float duration, Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        if (HasHitFlashTargets(flashRoots))
            SetHitFlashActive(flashRoots, false);
        onComplete?.Invoke();
    }

    private static IEnumerator CoTurnOffAfterDuration(GameObject root, float duration, Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        if (root != null)
            root.SetActive(false);
        onComplete?.Invoke();
    }

    /// <summary>Clears in-flight hit VFX/flash (e.g. when the map is stashed or re-shown after movement damage).</summary>
    public void ResetTransientPresentation()
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

        if (_poisonHitEffectRoutine != null)
        {
            StopCoroutine(_poisonHitEffectRoutine);
            _poisonHitEffectRoutine = null;
        }

        if (_poisonFlashRoutine != null)
        {
            StopCoroutine(_poisonFlashRoutine);
            _poisonFlashRoutine = null;
        }

        if (hitEffectRoot != null)
            hitEffectRoot.SetActive(false);
        if (poisonHitEffectRoot != null)
            poisonHitEffectRoot.SetActive(false);
        if (HasHitFlashTargets(hitFlashRoots))
            SetHitFlashActive(hitFlashRoots, false);
        if (HasHitFlashTargets(poisonHitFlashRoots))
            SetHitFlashActive(poisonHitFlashRoots, false);
    }

    private void OnDisable()
    {
        ResetTransientPresentation();
    }
}
