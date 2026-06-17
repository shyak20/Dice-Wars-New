using UnityEngine;

/// <summary>
/// Oscillates a <see cref="SpriteRenderer"/>'s alpha while preserving RGB from the color captured at startup.
/// </summary>
public sealed class SpriteRendererAlphaFlicker : MonoBehaviour
{
    public enum FlickerStyle
    {
        Sine = 0,
        Noise = 1
    }

    [Header("References")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Alpha range")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;

    [Header("Motion")]
    [SerializeField] private FlickerStyle style = FlickerStyle.Sine;
    [Tooltip("Cycles per second for sine flicker.")]
    [SerializeField, Min(0f)] private float frequency = 4f;
    [Tooltip("How fast the noise value drifts (higher = faster flicker).")]
    [SerializeField, Min(0.01f)] private float noiseScrollSpeed = 8f;
    [Tooltip("If true, uses unscaled time (ignores pause / time scale).")]
    [SerializeField] private bool useUnscaledTime;

    [Header("Timing")]
    [Tooltip("Optional phase offset in seconds (shifts the sine / noise sample).")]
    [SerializeField] private float phaseOffsetSeconds;

    private Color _baseRgbA;

    private void Awake()
    {
        if (targetRenderer == null)
            throw new System.InvalidOperationException("SpriteRendererAlphaFlicker requires targetRenderer.");

        if (minAlpha > maxAlpha)
            (minAlpha, maxAlpha) = (maxAlpha, minAlpha);

        _baseRgbA = targetRenderer.color;
    }

    private void OnDisable()
    {
        if (targetRenderer != null)
            targetRenderer.color = _baseRgbA;
    }

    private void Update()
    {
        var t = useUnscaledTime ? Time.unscaledTime : Time.time;
        t += phaseOffsetSeconds;

        float blend;
        if (style == FlickerStyle.Sine)
        {
            var w = Mathf.Max(0f, frequency) * Mathf.PI * 2f;
            blend = (Mathf.Sin(t * w) + 1f) * 0.5f;
        }
        else
        {
            var n = Mathf.PerlinNoise(t * noiseScrollSpeed, 0.72f);
            blend = n;
        }

        var c = _baseRgbA;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, blend);
        targetRenderer.color = c;
    }
}
