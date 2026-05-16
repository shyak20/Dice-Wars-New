using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Drives <c>_RevealAmount</c> and <c>_SplatterMaskOffset</c> on a UI <see cref="Graphic"/> material
/// (<c>DiceGame/UI Splatter Reveal (URP)</c>). Reveal and mask UV motion each have their own duration and easing curve.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SplatterRevealGraphicPlayer : MonoBehaviour
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

    [Header("Reveal playback")]
    [Tooltip("Reveal runs while this behaviour is enabled and the GameObject is active.")]
    [SerializeField] private bool playOnEnable = true;
    [FormerlySerializedAs("easing")]
    [SerializeField] private AnimationCurve revealEasing = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float revealDurationSeconds = 1.25f;
    [SerializeField] private bool useUnscaledTime = true;

    private Material _material;
    private Coroutine _routine;

    private void Awake()
    {
        if (graphic == null)
            graphic = GetComponent<Graphic>();

        if (graphic == null)
            throw new UnityException($"{nameof(SplatterRevealGraphicPlayer)} on '{name}' requires a Graphic (e.g. Image).");

        _material = graphic.material;
        if (_material == null)
            throw new UnityException($"{nameof(SplatterRevealGraphicPlayer)} on '{name}' expects an explicit Material on '{graphic.GetType().Name}' (assign a Material using shader 'DiceGame/UI Splatter Reveal (URP)').");

        if (!_material.HasProperty(ShaderPropertyIds.RevealAmount))
            throw new UnityException($"{nameof(SplatterRevealGraphicPlayer)} on '{name}': Material '{_material.name}' shader '{_material.shader.name}' is missing property '_RevealAmount'. Expected shader 'DiceGame/UI Splatter Reveal (URP)'.");

        if (!_material.HasProperty(ShaderPropertyIds.SplatterMaskOffset))
            throw new UnityException($"{nameof(SplatterRevealGraphicPlayer)} on '{name}': Material '{_material.name}' shader '{_material.shader.name}' is missing property '_SplatterMaskOffset'. Reimport/use shader 'DiceGame/UI Splatter Reveal (URP)'.");
    }

    private void OnDisable()
    {
        StopRevealRoutine();
        SetRevealImmediate(0f);
        ApplyMaskUvOffset(maskUvOffsetRevealStart);
    }

    private void OnEnable()
    {
        if (!playOnEnable)
            return;

        PlayReveal();
    }

    /// <summary>Starts (or restarts) reveal and mask-offset animations (each on its own duration/easing).</summary>
    public void PlayReveal()
    {
        StopRevealRoutine();
        ApplyMaskUvOffset(maskUvOffsetRevealStart);
        SetReveal(0f);
        _routine = StartCoroutine(CoReveal());
    }

    /// <summary>Sets reveal instantly without starting the coroutine. Does not change mask UV offset.</summary>
    public void SetRevealImmediate(float reveal01)
    {
        SetReveal(Mathf.Clamp01(reveal01));
    }

    /// <summary>Sets <c>_SplatterMaskOffset</c> XY immediately (does not animate).</summary>
    public void SetMaskUvOffsetImmediate(Vector2 xy)
    {
        ApplyMaskUvOffset(xy);
    }

    private void StopRevealRoutine()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator CoReveal()
    {
        var revealDone = revealDurationSeconds <= 0f;
        var maskDone = maskOffsetDurationSeconds <= 0f;

        if (revealDone)
            SetReveal(1f);
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
                    SetReveal(1f);
                }
                else
                {
                    var n = Mathf.Clamp01(tReveal / revealDurationSeconds);
                    SetReveal(Mathf.Clamp01(revealEasing.Evaluate(n)));
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

    private void SetReveal(float reveal01)
    {
        _material.SetFloat(ShaderPropertyIds.RevealAmount, reveal01);
    }

    private void ApplyMaskUvOffset(Vector2 xy)
    {
        _material.SetVector(ShaderPropertyIds.SplatterMaskOffset, new Vector4(xy.x, xy.y, 0f, 0f));
    }
}
