using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime-only helper: drives <c>_RevealAmount</c> on a dissolve material instance applied by
/// <see cref="EnemyCombatPresentationController"/> after the death animation delay.
/// </summary>
[DisallowMultipleComponent]
public sealed class SplatterRevealSpritePlayer : MonoBehaviour
{
    private static class ShaderPropertyIds
    {
        public static readonly int RevealAmount = Shader.PropertyToID("_RevealAmount");
    }

    SpriteRenderer _spriteRenderer;
    Material _targetMaterial;
    float _revealAmount = 1f;
    Coroutine _dissolveRoutine;

    /// <summary>Animates <paramref name="revealMaterialInstance"/> reveal from 1 → 0 over <paramref name="durationSeconds"/>.</summary>
    public bool PlayDissolveOut(SpriteRenderer spriteRenderer, float durationSeconds, Material revealMaterialInstance)
    {
        if (!Application.isPlaying)
            return false;

        _spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        _targetMaterial = revealMaterialInstance;

        if (_spriteRenderer == null)
        {
            Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': no SpriteRenderer.", this);
            return false;
        }

        if (_targetMaterial == null)
            _targetMaterial = _spriteRenderer.material;

        if (!MaterialSupportsReveal(_targetMaterial))
        {
            Debug.LogError(
                $"{nameof(SplatterRevealSpritePlayer)} on '{name}': dissolve material is missing _RevealAmount.",
                this);
            return false;
        }

        if (_dissolveRoutine != null)
            StopCoroutine(_dissolveRoutine);

        _dissolveRoutine = StartCoroutine(CoDissolveOut(Mathf.Max(0f, durationSeconds)));
        return true;
    }

    public void StopDissolve()
    {
        if (_dissolveRoutine != null)
        {
            StopCoroutine(_dissolveRoutine);
            _dissolveRoutine = null;
        }
    }

    IEnumerator CoDissolveOut(float durationSeconds)
    {
        _revealAmount = 1f;
        PushRevealAmount();

        if (durationSeconds <= 0f)
        {
            _revealAmount = 0f;
            PushRevealAmount();
            _dissolveRoutine = null;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.deltaTime;
            _revealAmount = 1f - Mathf.Clamp01(elapsed / durationSeconds);
            PushRevealAmount();
            yield return null;
        }

        _revealAmount = 0f;
        PushRevealAmount();
        _dissolveRoutine = null;
    }

    void PushRevealAmount()
    {
        var material = _targetMaterial;
        if (material == null && _spriteRenderer != null)
            material = _spriteRenderer.material;

        if (material == null || !material.HasProperty(ShaderPropertyIds.RevealAmount))
            return;

        material.SetFloat(ShaderPropertyIds.RevealAmount, _revealAmount);

        if (_spriteRenderer != null && _spriteRenderer.material != material)
            _spriteRenderer.material = material;
    }

    static bool MaterialSupportsReveal(Material material) =>
        material != null && material.HasProperty(ShaderPropertyIds.RevealAmount);
}
