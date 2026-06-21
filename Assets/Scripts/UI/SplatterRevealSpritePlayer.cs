using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Drives <c>_RevealAmount</c> (and optional <c>_SplatterMaskOffset</c>) on one or more
/// <see cref="SpriteRenderer"/> materials using <c>DiceGame/UI Splatter Reveal (URP)</c>.
/// Supports reveal-in (0 → 1), manual animator drive, and dissolve-out (1 → 0) for enemy death.
/// </summary>
[DisallowMultipleComponent]
public sealed class SplatterRevealSpritePlayer : MonoBehaviour
{
    sealed class SpriteRevealTarget
    {
        public SpriteRenderer Renderer;
        public Material RuntimeMaterial;
        public MaterialPropertyBlock PropertyBlock;
    }

    private static class ShaderPropertyIds
    {
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int RevealAmount = Shader.PropertyToID("_RevealAmount");
        public static readonly int SplatterMaskOffset = Shader.PropertyToID("_SplatterMaskOffset");
        public const string KeywordUiImage = "_SPLATTERRENDERTARGET_UI_IMAGE";
        public const string KeywordSpriteRenderer = "_SPLATTERRENDERTARGET_SPRITE_RENDERER";
    }

    [Header("Reveal in")]
    [SerializeField] private Material revealMaterialSource;
    [SerializeField] private bool includeAllChildSpriteRenderers = true;

    [Header("Splatter mask UV")]
    [SerializeField] private Vector2 maskUvOffsetRevealStart;
    [SerializeField] private Vector2 maskUvOffsetRevealTarget;
    [SerializeField] private AnimationCurve maskOffsetEasing = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float maskOffsetDurationSeconds = 1.25f;

    [Header("Manual reveal")]
    [Tooltip("When enabled, ignores reveal/mask animation settings and drives _RevealAmount from Manual Reveal only.")]
    [SerializeField] private bool manualSliderSet;
    [SerializeField, Range(0f, 1f)] private float manualReveal;

    [Header("Reveal playback")]
    [Tooltip("Reveal runs when this behaviour is enabled and a reveal material source is assigned.")]
    [SerializeField] private bool playOnEnable = true;
    [FormerlySerializedAs("easing")]
    [SerializeField] private AnimationCurve revealEasing = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float revealDurationSeconds = 2f;
    [SerializeField] private bool useUnscaledTime;

    readonly List<SpriteRevealTarget> _targets = new();
    Coroutine _routine;
    float _revealAmount;

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (manualSliderSet)
        {
            PushRevealAmount(manualReveal);
            return;
        }

        if (!playOnEnable || revealMaterialSource == null)
            return;

        PlayReveal();
    }

    void OnDisable()
    {
        StopRoutine();
        if (!manualSliderSet)
        {
            PushRevealAmount(0f);
            ApplyMaskUvOffset(maskUvOffsetRevealStart);
        }
    }

    void LateUpdate()
    {
        if (!manualSliderSet || !isActiveAndEnabled)
            return;

        PushRevealAmount(manualReveal);
    }

    void OnValidate()
    {
        if (!manualSliderSet)
            return;

        if (_targets.Count > 0)
            PushRevealAmount(manualReveal);
    }

    void OnDestroy()
    {
        StopRoutine();
        ReleaseAllTargets();
    }

    /// <summary>Clones splatter materials onto sprite renderers for manual <see cref="manualReveal"/> drive.</summary>
    /// <param name="scanRoot">
    /// When set, renderers are collected from this transform instead of this component's hierarchy.
    /// Use when portraits spawn under a world-space slot while this player stays on a canvas object for animator wiring.
    /// </param>
    public bool PrepareManualTargets(Material materialSource, Transform scanRoot = null)
    {
        if (materialSource == null)
        {
            Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': material source is null.", this);
            return false;
        }

        if (!MaterialSupportsReveal(materialSource))
        {
            Debug.LogError(
                $"{nameof(SplatterRevealSpritePlayer)} on '{name}': material '{materialSource.name}' is missing _RevealAmount.",
                this);
            return false;
        }

        if (!TrySetupTargets(materialSource, scanRoot, out var error))
        {
            Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': {error}", this);
            return false;
        }

        PushRevealAmount(manualReveal);
        return true;
    }

    /// <summary>Releases runtime materials and clears property blocks.</summary>
    public void ClearTargets() => ReleaseAllTargets();

    /// <summary>Sets reveal instantly when in manual mode.</summary>
    public void SetManualRevealImmediate(float reveal01)
    {
        manualReveal = Mathf.Clamp01(reveal01);
        if (manualSliderSet)
            PushRevealAmount(manualReveal);
    }

    /// <summary>Starts (or restarts) reveal-in on configured sprite renderers using <see cref="revealMaterialSource"/>.</summary>
    public bool PlayReveal()
    {
        if (!Application.isPlaying)
            return false;

        if (revealMaterialSource == null)
        {
            Debug.LogError(
                $"{nameof(SplatterRevealSpritePlayer)} on '{name}': assign {nameof(revealMaterialSource)} or call PlayReveal(Material).",
                this);
            return false;
        }

        return PlayReveal(revealMaterialSource);
    }

    /// <summary>Starts (or restarts) reveal-in using the given material asset as a clone source.</summary>
    public bool PlayReveal(Material materialSource)
    {
        if (!Application.isPlaying)
            return false;

        if (materialSource == null)
        {
            Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': material source is null.", this);
            return false;
        }

        if (!MaterialSupportsReveal(materialSource))
        {
            Debug.LogError(
                $"{nameof(SplatterRevealSpritePlayer)} on '{name}': material '{materialSource.name}' is missing _RevealAmount.",
                this);
            return false;
        }

        if (!TrySetupTargets(materialSource, scanRoot: null, out var error))
        {
            Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': {error}", this);
            return false;
        }

        StopRoutine();
        ApplyMaskUvOffset(maskUvOffsetRevealStart);
        PushRevealAmount(EvaluateRevealEasing(0f));
        _routine = StartCoroutine(CoRevealIn());
        return true;
    }

    /// <summary>Dissolves all child sprite renderers from reveal 1 → 0.</summary>
    public bool PlayDissolveOut(float durationSeconds, Material dissolveMaterialSource) =>
        PlayDissolveOut(null, durationSeconds, dissolveMaterialSource);

    /// <summary>
    /// Instantiates a copy of <paramref name="dissolveMaterialSource"/>, applies it to sprite renderers, and animates reveal 1 → 0.
    /// When <paramref name="spriteRenderer"/> is null, all child renderers are used when <see cref="includeAllChildSpriteRenderers"/> is true.
    /// </summary>
    public bool PlayDissolveOut(SpriteRenderer spriteRenderer, float durationSeconds, Material dissolveMaterialSource)
    {
        if (!Application.isPlaying)
            return false;

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

        StopRoutine();
        ReleaseAllTargets();

        if (spriteRenderer != null)
        {
            if (!TryAddTarget(renderer: spriteRenderer, dissolveMaterialSource, out var target, out var error))
            {
                Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': {error}", this);
                return false;
            }

            _targets.Add(target);
        }
        else if (!TrySetupTargets(dissolveMaterialSource, scanRoot: null, out var setupError))
        {
            Debug.LogError($"{nameof(SplatterRevealSpritePlayer)} on '{name}': {setupError}", this);
            return false;
        }

        _revealAmount = 1f;
        PushRevealAmount(_revealAmount);
        _routine = StartCoroutine(CoDissolveOut(Mathf.Max(0f, durationSeconds)));
        return true;
    }

    public void StopDissolve()
    {
        StopRoutine();
        for (var i = 0; i < _targets.Count; i++)
        {
            if (_targets[i].Renderer != null)
                _targets[i].Renderer.SetPropertyBlock(null);
        }
    }

    /// <summary>Destroys the runtime material instance after the sprite has been switched back to its default material.</summary>
    public void DisposeOwnedMaterial() => ReleaseAllTargets();

    bool TrySetupTargets(Material materialSource, Transform scanRoot, out string error)
    {
        error = null;
        ReleaseAllTargets();

        var renderers = CollectSpriteRenderers(scanRoot);
        if (renderers.Count == 0)
        {
            error = scanRoot != null
                ? $"no SpriteRenderer targets found under '{scanRoot.name}'."
                : "no SpriteRenderer targets found.";
            return false;
        }

        foreach (var renderer in renderers)
        {
            if (!TryAddTarget(renderer, materialSource, out var target, out error))
                return false;

            _targets.Add(target);
        }

        return true;
    }

    List<SpriteRenderer> CollectSpriteRenderers(Transform scanRoot)
    {
        var renderers = new List<SpriteRenderer>();
        if (scanRoot != null)
        {
            scanRoot.GetComponentsInChildren(true, renderers);
            renderers.RemoveAll(r => r == null || r.sprite == null);
            return renderers;
        }

        if (includeAllChildSpriteRenderers)
        {
            GetComponentsInChildren(true, renderers);
            renderers.RemoveAll(r => r == null || r.sprite == null);
            return renderers;
        }

        var local = GetComponent<SpriteRenderer>();
        if (local != null && local.sprite != null)
            renderers.Add(local);

        return renderers;
    }

    static bool TryAddTarget(SpriteRenderer renderer, Material source, out SpriteRevealTarget target, out string error)
    {
        target = null;
        error = null;

        if (renderer == null)
        {
            error = "SpriteRenderer is null.";
            return false;
        }

        if (!TryCreateAndAssignRuntimeMaterial(renderer, source, out var runtimeMaterial))
        {
            error = $"failed to create runtime material for '{renderer.name}'.";
            return false;
        }

        target = new SpriteRevealTarget
        {
            Renderer = renderer,
            RuntimeMaterial = runtimeMaterial,
            PropertyBlock = new MaterialPropertyBlock()
        };
        return true;
    }

    IEnumerator CoRevealIn()
    {
        var revealDone = revealDurationSeconds <= 0f;
        var maskDone = maskOffsetDurationSeconds <= 0f;

        if (revealDone)
            PushRevealAmount(EvaluateRevealEasing(1f));
        if (maskDone)
            ApplyMaskUvOffset(maskUvOffsetRevealTarget);

        if (revealDone && maskDone)
        {
            _routine = null;
            yield break;
        }

        var tReveal = 0f;
        var tMask = 0f;

        while (!revealDone || !maskDone)
        {
            var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (!revealDone)
            {
                tReveal += dt;
                if (tReveal >= revealDurationSeconds)
                {
                    revealDone = true;
                    PushRevealAmount(EvaluateRevealEasing(1f));
                }
                else
                {
                    var n = Mathf.Clamp01(tReveal / revealDurationSeconds);
                    PushRevealAmount(EvaluateRevealEasing(n));
                }
            }

            if (!maskDone)
            {
                tMask += dt;
                if (tMask >= maskOffsetDurationSeconds)
                {
                    maskDone = true;
                    ApplyMaskUvOffset(maskUvOffsetRevealTarget);
                }
                else
                {
                    var n = Mathf.Clamp01(tMask / maskOffsetDurationSeconds);
                    var v = Mathf.Clamp01(maskOffsetEasing.Evaluate(n));
                    ApplyMaskUvOffset(Vector2.Lerp(maskUvOffsetRevealStart, maskUvOffsetRevealTarget, v));
                }
            }

            yield return null;
        }

        _routine = null;
    }

    IEnumerator CoDissolveOut(float durationSeconds)
    {
        _revealAmount = 1f;
        PushRevealAmount(_revealAmount);

        if (durationSeconds <= 0f)
        {
            _revealAmount = 0f;
            PushRevealAmount(_revealAmount);
            _routine = null;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.deltaTime;
            _revealAmount = 1f - Mathf.Clamp01(elapsed / durationSeconds);
            PushRevealAmount(_revealAmount);
            yield return null;
        }

        _revealAmount = 0f;
        PushRevealAmount(_revealAmount);
        _routine = null;
    }

    float EvaluateRevealEasing(float normalizedTime) =>
        Mathf.Clamp01(revealEasing.Evaluate(Mathf.Clamp01(normalizedTime)));

    void PushRevealAmount(float reveal01)
    {
        _revealAmount = Mathf.Clamp01(reveal01);

        for (var i = 0; i < _targets.Count; i++)
        {
            var target = _targets[i];
            if (target.RuntimeMaterial != null && target.RuntimeMaterial.HasProperty(ShaderPropertyIds.RevealAmount))
                target.RuntimeMaterial.SetFloat(ShaderPropertyIds.RevealAmount, _revealAmount);

            if (target.Renderer == null)
                continue;

            target.PropertyBlock ??= new MaterialPropertyBlock();
            target.Renderer.GetPropertyBlock(target.PropertyBlock);
            target.PropertyBlock.SetFloat(ShaderPropertyIds.RevealAmount, _revealAmount);
            target.Renderer.SetPropertyBlock(target.PropertyBlock);
        }
    }

    void ApplyMaskUvOffset(Vector2 xy)
    {
        var value = new Vector4(xy.x, xy.y, 0f, 0f);
        for (var i = 0; i < _targets.Count; i++)
        {
            var material = _targets[i].RuntimeMaterial;
            if (material != null && material.HasProperty(ShaderPropertyIds.SplatterMaskOffset))
                material.SetVector(ShaderPropertyIds.SplatterMaskOffset, value);
        }
    }

    void StopRoutine()
    {
        if (_routine == null)
            return;

        StopCoroutine(_routine);
        _routine = null;
    }

    void ReleaseAllTargets()
    {
        for (var i = 0; i < _targets.Count; i++)
        {
            var target = _targets[i];
            if (target.Renderer != null)
                target.Renderer.SetPropertyBlock(null);

            if (target.RuntimeMaterial == null)
                continue;

            if (Application.isPlaying)
                Destroy(target.RuntimeMaterial);
            else
                DestroyImmediate(target.RuntimeMaterial);
        }

        _targets.Clear();
    }

    static bool TryCreateAndAssignRuntimeMaterial(SpriteRenderer spriteRenderer, Material source, out Material runtimeMaterial)
    {
        runtimeMaterial = new Material(source);
        runtimeMaterial.shaderKeywords = source.shaderKeywords;
        ApplySpriteRendererRenderTarget(runtimeMaterial);
        BindSpriteToMaterial(spriteRenderer, runtimeMaterial);
        runtimeMaterial.SetFloat(ShaderPropertyIds.RevealAmount, 0f);

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

    static bool MaterialSupportsReveal(Material material) =>
        material != null && material.HasProperty(ShaderPropertyIds.RevealAmount);

    static void ApplySpriteRendererRenderTarget(Material material)
    {
        if (material == null)
            return;

        material.DisableKeyword(ShaderPropertyIds.KeywordUiImage);
        material.EnableKeyword(ShaderPropertyIds.KeywordSpriteRenderer);
    }
}
