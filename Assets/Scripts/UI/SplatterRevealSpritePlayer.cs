using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime-only helper: clones the dissolve material, applies it to the enemy sprite, and drives
/// <c>_RevealAmount</c> from 1 → 0 after the death animation delay.
/// </summary>
[DisallowMultipleComponent]
public sealed class SplatterRevealSpritePlayer : MonoBehaviour
{
    private static class ShaderPropertyIds
    {
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int RevealAmount = Shader.PropertyToID("_RevealAmount");
        public static readonly int SplatterMaskOffset = Shader.PropertyToID("_SplatterMaskOffset");
    }

    SpriteRenderer _spriteRenderer;
    Material _runtimeMaterial;
    MaterialPropertyBlock _propertyBlock;
    float _revealAmount = 1f;
    Coroutine _dissolveRoutine;

    /// <summary>
    /// Instantiates a copy of <paramref name="dissolveMaterialSource"/>, applies it to the sprite, and animates reveal 1 → 0.
    /// </summary>
    public bool PlayDissolveOut(SpriteRenderer spriteRenderer, float durationSeconds, Material dissolveMaterialSource)
    {
        if (!Application.isPlaying)
            return false;

        _spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null)
        {
            Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': no SpriteRenderer.", this);
            return false;
        }

        if (dissolveMaterialSource == null)
        {
            Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': dissolve material source is null.", this);
            return false;
        }

        if (!MaterialSupportsReveal(dissolveMaterialSource))
        {
            Debug.LogError(
                $"{nameof(SplatterRevealSpritePlayer)} on '{name}': dissolve material is missing _RevealAmount.",
                this);
            return false;
        }

        if (_dissolveRoutine != null)
            StopCoroutine(_dissolveRoutine);

        if (!TryCreateAndAssignRuntimeMaterial(_spriteRenderer, dissolveMaterialSource, out _runtimeMaterial))
            return false;

        _revealAmount = 1f;
        PushRevealAmount();

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

        ClearPropertyBlock();
    }

    /// <summary>Destroys the runtime material instance after the sprite has been switched back to its default material.</summary>
    public void DisposeOwnedMaterial()
    {
        ReleaseRuntimeMaterial();
    }

    void OnDestroy()
    {
        ClearPropertyBlock();
        ReleaseRuntimeMaterial();
    }

    static bool TryCreateAndAssignRuntimeMaterial(SpriteRenderer spriteRenderer, Material source, out Material runtimeMaterial)
    {
        runtimeMaterial = new Material(source);
        runtimeMaterial.shaderKeywords = source.shaderKeywords;
        BindSpriteToMaterial(spriteRenderer, runtimeMaterial);
        runtimeMaterial.SetFloat(ShaderPropertyIds.RevealAmount, 1f);

        // Assign the owned instance via sharedMaterial — using .material here can clone again so
        // property updates on runtimeMaterial would not match what the renderer draws.
        spriteRenderer.SetPropertyBlock(null);
        spriteRenderer.sharedMaterial = runtimeMaterial;
        return true;
    }

    static void BindSpriteToMaterial(SpriteRenderer spriteRenderer, Material material)
    {
        if (spriteRenderer == null || material == null)
            return;

        var sprite = spriteRenderer.sprite;
        if (sprite != null)
        {
            if (material.HasProperty(ShaderPropertyIds.MainTex))
                material.SetTexture(ShaderPropertyIds.MainTex, sprite.texture);
            material.mainTexture = sprite.texture;
        }

        if (material.HasProperty(ShaderPropertyIds.Color))
            material.SetColor(ShaderPropertyIds.Color, spriteRenderer.color);
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
        if (_runtimeMaterial != null && _runtimeMaterial.HasProperty(ShaderPropertyIds.RevealAmount))
            _runtimeMaterial.SetFloat(ShaderPropertyIds.RevealAmount, _revealAmount);

        if (_spriteRenderer == null)
            return;

        _propertyBlock ??= new MaterialPropertyBlock();
        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(ShaderPropertyIds.RevealAmount, _revealAmount);
        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }

    void ClearPropertyBlock()
    {
        if (_spriteRenderer != null)
            _spriteRenderer.SetPropertyBlock(null);
    }

    void ReleaseRuntimeMaterial()
    {
        if (_runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(_runtimeMaterial);
        else
            DestroyImmediate(_runtimeMaterial);

        _runtimeMaterial = null;
    }

    static bool MaterialSupportsReveal(Material material) =>
        material != null && material.HasProperty(ShaderPropertyIds.RevealAmount);
}
