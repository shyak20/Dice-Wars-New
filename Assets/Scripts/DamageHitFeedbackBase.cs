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

    [Header("Camera shake (scaled by hit severity)")]
    [Tooltip("Severity = HP lost / max HP, or if armor absorbed all damage, incoming damage / max HP (each clamped 0–1).")]
    [SerializeField] private AnimationCurve damageRatioToShakeBlend = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float minShakeDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float maxShakeDuration = 0.32f;
    [SerializeField, Min(0f)] private float minShakeMagnitude = 0.02f;
    [SerializeField, Min(0f)] private float maxShakeMagnitude = 0.28f;

    private Coroutine _hitEffectRoutine;
    private Coroutine _flashRoutine;

    protected virtual void Awake()
    {
        if (hitEffectRoot != null)
            hitEffectRoot.SetActive(false);
        if (HasHitFlashTargets())
            SetHitFlashActive(false);
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

    protected void PlayHit(int grossDamage, int hpLost, int maxHp)
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

        if (hitEffectRoot != null)
        {
            if (_hitEffectRoutine != null)
                StopCoroutine(_hitEffectRoutine);
            hitEffectRoot.SetActive(true);
            _hitEffectRoutine = StartCoroutine(CoTurnOffAfterDuration(hitEffectRoot, hitEffectDuration, () => _hitEffectRoutine = null));
        }

        if (HasHitFlashTargets())
        {
            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            SetHitFlashActive(true);
            _flashRoutine = StartCoroutine(CoTurnOffHitFlash());
        }
    }

    private bool HasHitFlashTargets()
    {
        if (hitFlashRoots == null || hitFlashRoots.Count == 0)
            return false;

        for (var i = 0; i < hitFlashRoots.Count; i++)
        {
            if (hitFlashRoots[i] != null)
                return true;
        }

        return false;
    }

    private void SetHitFlashActive(bool active)
    {
        if (hitFlashRoots == null)
            return;

        for (var i = 0; i < hitFlashRoots.Count; i++)
        {
            var root = hitFlashRoots[i];
            if (root == null)
                throw new InvalidOperationException($"DamageHitFeedbackBase on '{name}': hit flash root index {i} is null.");
            root.SetActive(active);
        }
    }

    private IEnumerator CoTurnOffHitFlash()
    {
        yield return new WaitForSeconds(hitFlashDuration);
        SetHitFlashActive(false);
        _flashRoutine = null;
    }

    private static IEnumerator CoTurnOffAfterDuration(GameObject root, float duration, Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        if (root != null)
            root.SetActive(false);
        onComplete?.Invoke();
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
        if (HasHitFlashTargets())
            SetHitFlashActive(false);
    }
}
