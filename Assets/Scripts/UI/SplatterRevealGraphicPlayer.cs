using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Drives <c>_RevealAmount</c> and <c>_SplatterMaskOffset</c> on a UI <see cref="Graphic"/> material
/// (<c>DiceGame/UI Splatter Reveal (URP)</c>). Reveal and mask UV motion each have their own duration and easing curve.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
[ExecuteAlways]
public sealed class SplatterRevealGraphicPlayer : MonoBehaviour, IMaterialModifier
{
    private static class ShaderPropertyIds
    {
        public static readonly int RevealAmount = Shader.PropertyToID("_RevealAmount");
        public static readonly int SplatterMaskOffset = Shader.PropertyToID("_SplatterMaskOffset");
    }

    [Header("Target")]
    [SerializeField] private Graphic graphic;

    [Header("Splatter mask UV")]
    [Tooltip("Beginning XY added to splash sampling UVs (_SplatterMaskOffset) when playback starts.")]
    [SerializeField] private Vector2 maskUvOffsetRevealStart;
    [Tooltip("Target XY reached when mask-offset eased progress reaches 1.")]
    [SerializeField] private Vector2 maskUvOffsetRevealTarget;
    [SerializeField] private AnimationCurve maskOffsetEasing = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float maskOffsetDurationSeconds = 1.25f;

    [Header("Manual reveal")]
    [Tooltip("When enabled, ignores reveal/mask animation settings and drives _RevealAmount from Manual Reveal only.")]
    [SerializeField] private bool manualSliderSet;
    [SerializeField, Range(0f, 1f)] private float manualReveal;

    [Header("Reveal playback")]
    [Tooltip("Reveal runs while this behaviour is enabled and the GameObject is active.")]
    [SerializeField] private bool playOnEnable = true;
    [FormerlySerializedAs("easing")]
    [SerializeField] private AnimationCurve revealEasing = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float revealDurationSeconds = 1.25f;
    [SerializeField] private bool useUnscaledTime = true;

    Material _material;
    Coroutine _routine;

    void Awake()
    {
        ResolveGraphic();
        ValidateMaterial();
    }

    void OnDisable()
    {
        StopRevealRoutine();
        if (!manualSliderSet)
        {
            PushRevealAmount(0f);
            ApplyMaskUvOffset(maskUvOffsetRevealStart);
        }
    }

    void OnEnable()
    {
        ResolveGraphic();

        if (manualSliderSet)
        {
            PushRevealAmount(manualReveal);
            return;
        }

        if (!playOnEnable)
            return;

        PlayReveal();
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

        ResolveGraphic();
        if (_material != null || TryCacheMaterial())
            PushRevealAmount(manualReveal);
    }

    /// <summary>Starts (or restarts) reveal and mask-offset animations (each on its own duration/easing).</summary>
    public void PlayReveal()
    {
        if (manualSliderSet)
        {
            PushRevealAmount(manualReveal);
            return;
        }

        StopRevealRoutine();
        ApplyMaskUvOffset(maskUvOffsetRevealStart);
        PushRevealAmount(EvaluateRevealEasing(0f));
        _routine = StartCoroutine(CoReveal());
    }

    /// <summary>Sets reveal instantly without starting the coroutine. Does not change mask UV offset.</summary>
    public void SetRevealImmediate(float reveal01) => PushRevealAmount(reveal01);

    /// <summary>Sets <c>_SplatterMaskOffset</c> XY immediately (does not animate).</summary>
    public void SetMaskUvOffsetImmediate(Vector2 xy) => ApplyMaskUvOffset(xy);

    /// <inheritdoc />
    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!manualSliderSet || baseMaterial == null || !baseMaterial.HasProperty(ShaderPropertyIds.RevealAmount))
            return baseMaterial;

        baseMaterial.SetFloat(ShaderPropertyIds.RevealAmount, Mathf.Clamp01(manualReveal));
        return baseMaterial;
    }

    void StopRevealRoutine()
    {
        if (_routine == null)
            return;

        StopCoroutine(_routine);
        _routine = null;
    }

    IEnumerator CoReveal()
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

    float EvaluateRevealEasing(float normalizedTime) =>
        Mathf.Clamp01(revealEasing.Evaluate(Mathf.Clamp01(normalizedTime)));

    void PushRevealAmount(float reveal01)
    {
        if (!TryCacheMaterial())
            return;

        var value = Mathf.Clamp01(reveal01);
        _material.SetFloat(ShaderPropertyIds.RevealAmount, value);

        if (graphic != null && graphic.IsActive() && graphic.canvas != null)
        {
            var renderingMaterial = graphic.materialForRendering;
            if (renderingMaterial != null && !ReferenceEquals(renderingMaterial, _material))
                renderingMaterial.SetFloat(ShaderPropertyIds.RevealAmount, value);

            graphic.SetMaterialDirty();
        }
    }

    void ApplyMaskUvOffset(Vector2 xy)
    {
        if (!TryCacheMaterial())
            return;

        _material.SetVector(ShaderPropertyIds.SplatterMaskOffset, new Vector4(xy.x, xy.y, 0f, 0f));
        graphic.SetMaterialDirty();
    }

    void ResolveGraphic()
    {
        var localGraphic = GetComponent<Graphic>();
        if (localGraphic == null)
            throw new UnityException($"{nameof(SplatterRevealGraphicPlayer)} on '{name}' requires a Graphic (e.g. Image).");

        if (graphic != null && graphic != localGraphic)
        {
            Debug.LogWarning(
                $"{nameof(SplatterRevealGraphicPlayer)} on '{name}': Graphic reference pointed at '{graphic.name}' on another object; using '{localGraphic.name}' on this object instead.",
                this);
        }

        graphic = localGraphic;
    }

    void ValidateMaterial()
    {
        if (!TryCacheMaterial())
            throw new UnityException(
                $"{nameof(SplatterRevealGraphicPlayer)} on '{name}' expects an explicit Material on '{graphic.GetType().Name}' (assign a Material using shader 'DiceGame/UI Splatter Reveal (URP)').");

        if (!_material.HasProperty(ShaderPropertyIds.RevealAmount))
            throw new UnityException(
                $"{nameof(SplatterRevealGraphicPlayer)} on '{name}': Material '{_material.name}' shader '{_material.shader.name}' is missing property '_RevealAmount'. Expected shader 'DiceGame/UI Splatter Reveal (URP)'.");

        if (!_material.HasProperty(ShaderPropertyIds.SplatterMaskOffset))
            throw new UnityException(
                $"{nameof(SplatterRevealGraphicPlayer)} on '{name}': Material '{_material.name}' shader '{_material.shader.name}' is missing property '_SplatterMaskOffset'. Reimport/use shader 'DiceGame/UI Splatter Reveal (URP)'.");
    }

    bool TryCacheMaterial()
    {
        if (_material != null)
            return true;

        if (graphic == null)
            ResolveGraphic();

        if (graphic == null)
            return false;

        _material = graphic.material;
        return _material != null;
    }
}
