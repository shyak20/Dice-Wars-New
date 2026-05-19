using System.Collections;
using UnityEngine;

/// <summary>
/// Fades RealToon <c>Cutout</c> (<c>_Cutout</c>) from 0 (visible) to 1 (hidden) on die renderers.
/// Disabled on the dice prefab; enabled only when dissolving after a roll. Face materials must be assigned first
/// (see <see cref="DieVisualizer.Initialize"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class DissolveFadeController : MonoBehaviour
{
    public const string RealToonCutoutPropertyName = "_Cutout";
    public const string RealToonCutoutKeyword = "N_F_CO_ON";

    private static readonly int CutoutId = Shader.PropertyToID(RealToonCutoutPropertyName);
    private static readonly int CutoutFeatureToggleId = Shader.PropertyToID("_N_F_CO");

    [Tooltip("Renderers to fade. If empty, uses the DieVisualizer mesh renderer or all child renderers.")]
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
            if (isActiveAndEnabled)
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

    private void OnEnable()
    {
        CacheRenderers();
        if (!ValidateRealToonMaterials())
            return;

        ApplyCutoutAmount();
    }

    private void Start()
    {
        if (!isActiveAndEnabled || !animateToCutoutOnStart)
            return;

        var target = cutoutAmount;
        cutoutAmount = 0f;
        ApplyCutoutAmount();
        FadeTo(target, fadeDurationSeconds);
    }

    private bool ValidateRealToonMaterials()
    {
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogError($"DissolveFadeController on '{name}': no renderers to fade.", this);
            return false;
        }

        var valid = false;
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var shared = renderer.sharedMaterials;
            for (var m = 0; m < shared.Length; m++)
            {
                var mat = shared[m];
                if (mat == null)
                    continue;

                if (!mat.HasProperty(CutoutId))
                {
                    Debug.LogError(
                        $"DissolveFadeController on '{name}': renderer '{renderer.name}' material slot {m} must use RealToon with Cutout ({RealToonCutoutPropertyName}).",
                        renderer);
                    continue;
                }

                valid = true;
            }
        }

        if (!valid)
        {
            Debug.LogError(
                $"DissolveFadeController on '{name}': no RealToon cutout materials found. Assign face materials before enabling dissolve.",
                this);
        }

        return valid;
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

    /// <summary>Re-scan renderers (call after <see cref="DieVisualizer.Initialize"/> assigns face materials).</summary>
    public void RefreshRenderers()
    {
        renderers = null;
        CacheRenderers();
        if (!isActiveAndEnabled)
            return;

        ValidateRealToonMaterials();
        ApplyCutoutAmount();
    }

    private void CacheRenderers()
    {
        var visualizer = GetComponent<DieVisualizer>();
        if (visualizer != null && visualizer.meshRenderer != null)
        {
            renderers = new[] { visualizer.meshRenderer };
            return;
        }

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
        if (!isActiveAndEnabled || renderers == null || renderers.Length == 0)
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
