using System.Collections;
using UnityEngine;

/// <summary>
/// Fades RealToon <c>Cutout</c> (<c>_Cutout</c>) from 0 (visible) to 1 (hidden) on child renderers.
/// </summary>
[DisallowMultipleComponent]
public sealed class DissolveFadeController : MonoBehaviour
{
    public const string RealToonCutoutPropertyName = "_Cutout";
    public const string RealToonCutoutKeyword = "N_F_CO_ON";

    private static readonly int CutoutId = Shader.PropertyToID(RealToonCutoutPropertyName);
    private static readonly int CutoutFeatureToggleId = Shader.PropertyToID("_N_F_CO");

    [Tooltip("Renderers to fade. If empty, uses all child renderers (including inactive).")]
    [SerializeField] private Renderer[] renderers;

    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Fade")]
    [Tooltip("Seconds to reach the target cutout when fading. 0 = instant.")]
    [SerializeField, Min(0f)] private float fadeDurationSeconds = 1f;

    [Tooltip("On Start, fades from fully visible (0) to Cutout below.")]
    [SerializeField] private bool animateToCutoutOnStart;

    [SerializeField, Range(0f, 1f)] private float cutoutAmount;

    private Coroutine _fadeRoutine;

    /// <summary>0 = fully shown, 1 = fully cut out.</summary>
    public float CutoutAmount
    {
        get => cutoutAmount;
        set
        {
            cutoutAmount = Mathf.Clamp01(value);
            ApplyCutoutAmount();
        }
    }

    /// <summary>Seconds used by <see cref="FadeOut()"/>, <see cref="FadeIn()"/>, and <see cref="FadeTo(float)"/>.</summary>
    public float FadeDurationSeconds
    {
        get => fadeDurationSeconds;
        set => fadeDurationSeconds = Mathf.Max(0f, value);
    }

    private void Reset()
    {
        CacheRenderers();
    }

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            CacheRenderers();

        ValidateRealToonMaterials();
        ApplyCutoutAmount();
    }

    private void Start()
    {
        if (!animateToCutoutOnStart)
            return;

        var target = cutoutAmount;
        cutoutAmount = 0f;
        ApplyCutoutAmount();
        FadeTo(target, fadeDurationSeconds);
    }

    private void ValidateRealToonMaterials()
    {
        if (renderers == null)
            return;

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var shared = renderer.sharedMaterials;
            for (var m = 0; m < shared.Length; m++)
            {
                var mat = shared[m];
                if (mat == null || !mat.HasProperty(CutoutId))
                {
                    Debug.LogError(
                        $"DissolveFadeController on '{name}': renderer '{renderer.name}' material slot {m} must use RealToon with Cutout ({RealToonCutoutPropertyName}).",
                        renderer);
                }
            }
        }
    }

    private void OnValidate()
    {
        cutoutAmount = Mathf.Clamp01(cutoutAmount);
        if (isActiveAndEnabled)
            ApplyCutoutAmount();
    }

    /// <summary>Fades from current cutout to fully hidden (1).</summary>
    public void FadeOut() => FadeTo(1f, fadeDurationSeconds);

    /// <summary>Fades from current cutout to fully shown (0).</summary>
    public void FadeIn() => FadeTo(0f, fadeDurationSeconds);

    /// <summary>Fades to <paramref name="targetAmount"/> over <see cref="fadeDurationSeconds"/>.</summary>
    public void FadeTo(float targetAmount) => FadeTo(targetAmount, fadeDurationSeconds);

    public void FadeOut(float durationSeconds) => FadeTo(1f, durationSeconds);

    public void FadeIn(float durationSeconds) => FadeTo(0f, durationSeconds);

    public void FadeTo(float targetAmount, float durationSeconds)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        if (durationSeconds <= 0f)
        {
            CutoutAmount = targetAmount;
            return;
        }

        _fadeRoutine = StartCoroutine(CoFadeTo(targetAmount, durationSeconds));
    }

    public void StopFade()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }

    /// <summary>Immediately visible (cutout 0).</summary>
    public void ShowImmediate()
    {
        StopFade();
        CutoutAmount = 0f;
    }

    /// <summary>Immediately hidden (cutout 1).</summary>
    public void HideImmediate()
    {
        StopFade();
        CutoutAmount = 1f;
    }

    private IEnumerator CoFadeTo(float target, float duration)
    {
        var start = cutoutAmount;
        var elapsed = 0f;
        target = Mathf.Clamp01(target);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            CutoutAmount = Mathf.Lerp(start, target, t);
            yield return null;
        }

        CutoutAmount = target;
        _fadeRoutine = null;
    }

    /// <summary>Re-scan child renderers (e.g. after face materials are assigned).</summary>
    public void RefreshRenderers()
    {
        CacheRenderers();
        ValidateRealToonMaterials();
        ApplyCutoutAmount();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
    }

    private static void EnableRealToonCutout(Material material)
    {
        material.EnableKeyword(RealToonCutoutKeyword);
        if (material.HasProperty(CutoutFeatureToggleId))
            material.SetFloat(CutoutFeatureToggleId, 1f);
    }

    private void ApplyCutoutAmount()
    {
        if (renderers == null || renderers.Length == 0)
            return;

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var materials = renderer.materials;
            for (var m = 0; m < materials.Length; m++)
            {
                var mat = materials[m];
                if (mat == null || !mat.HasProperty(CutoutId))
                    continue;

                EnableRealToonCutout(mat);
                mat.SetFloat(CutoutId, cutoutAmount);
            }
        }
    }
}
