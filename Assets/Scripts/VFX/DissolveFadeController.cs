using System.Collections;
using UnityEngine;

/// <summary>
/// Drives <c>_DissolveAmount</c> on DiceWars/Dissolve Lit materials: 0 = fully visible, 1 = fully hidden.
/// </summary>
[DisallowMultipleComponent]
public sealed class DissolveFadeController : MonoBehaviour
{
    public const string DissolveAmountPropertyName = "_DissolveAmount";

    private static readonly int DissolveAmountId = Shader.PropertyToID(DissolveAmountPropertyName);

    [Tooltip("Renderers to fade. If empty, uses all child renderers (including inactive).")]
    [SerializeField] private Renderer[] renderers;

    [SerializeField] private bool includeInactiveChildren = true;

    [Tooltip("When enabled, uses MaterialPropertyBlock so shared materials are not instanced.")]
    [SerializeField] private bool useMaterialPropertyBlock = true;

    [Header("Fade")]
    [Tooltip("Seconds to reach the target dissolve amount when fading. 0 = instant.")]
    [SerializeField, Min(0f)] private float fadeDurationSeconds = 1f;

    [Tooltip("On Start, fades from fully visible (0) to Dissolve Amount below.")]
    [SerializeField] private bool animateToDissolveAmountOnStart;

    [SerializeField, Range(0f, 1f)] private float dissolveAmount;

    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _fadeRoutine;

    /// <summary>0 = fully shown, 1 = fully hidden.</summary>
    public float DissolveAmount
    {
        get => dissolveAmount;
        set
        {
            dissolveAmount = Mathf.Clamp01(value);
            ApplyDissolveAmount();
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

        _propertyBlock = new MaterialPropertyBlock();
        ValidateDissolveMaterials();
        ApplyDissolveAmount();
    }

    private void Start()
    {
        if (!animateToDissolveAmountOnStart)
            return;

        var target = dissolveAmount;
        dissolveAmount = 0f;
        ApplyDissolveAmount();
        FadeTo(target, fadeDurationSeconds);
    }

    private void ValidateDissolveMaterials()
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
                if (mat == null || !mat.HasProperty(DissolveAmountId))
                {
                    Debug.LogError(
                        $"DissolveFadeController on '{name}': renderer '{renderer.name}' material slot {m} must use 'DiceWars/Dissolve Lit (URP)'.",
                        renderer);
                }
            }
        }
    }

    private void OnValidate()
    {
        dissolveAmount = Mathf.Clamp01(dissolveAmount);
        if (isActiveAndEnabled)
            ApplyDissolveAmount();
    }

    /// <summary>Fades from current amount to fully hidden (1) over <see cref="fadeDurationSeconds"/>.</summary>
    public void FadeOut() => FadeTo(1f, fadeDurationSeconds);

    /// <summary>Fades from current amount to fully shown (0) over <see cref="fadeDurationSeconds"/>.</summary>
    public void FadeIn() => FadeTo(0f, fadeDurationSeconds);

    /// <summary>Fades to <paramref name="targetAmount"/> over <see cref="fadeDurationSeconds"/>.</summary>
    public void FadeTo(float targetAmount) => FadeTo(targetAmount, fadeDurationSeconds);

    /// <summary>Fades from current amount to fully hidden (1).</summary>
    public void FadeOut(float durationSeconds) => FadeTo(1f, durationSeconds);

    /// <summary>Fades from current amount to fully shown (0).</summary>
    public void FadeIn(float durationSeconds) => FadeTo(0f, durationSeconds);

    /// <summary>Fades to a target dissolve amount over time.</summary>
    public void FadeTo(float targetAmount, float durationSeconds)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        if (durationSeconds <= 0f)
        {
            DissolveAmount = targetAmount;
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

    /// <summary>Immediately visible.</summary>
    public void ShowImmediate()
    {
        StopFade();
        DissolveAmount = 0f;
    }

    /// <summary>Immediately hidden (dissolved).</summary>
    public void HideImmediate()
    {
        StopFade();
        DissolveAmount = 1f;
    }

    private IEnumerator CoFadeTo(float target, float duration)
    {
        var start = dissolveAmount;
        var elapsed = 0f;
        target = Mathf.Clamp01(target);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            DissolveAmount = Mathf.Lerp(start, target, t);
            yield return null;
        }

        DissolveAmount = target;
        _fadeRoutine = null;
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
    }

    private void ApplyDissolveAmount()
    {
        if (renderers == null || renderers.Length == 0)
            return;

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            if (useMaterialPropertyBlock)
            {
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(DissolveAmountId, dissolveAmount);
                renderer.SetPropertyBlock(_propertyBlock);
            }
            else
            {
                var materials = renderer.materials;
                for (var m = 0; m < materials.Length; m++)
                {
                    if (materials[m] != null && materials[m].HasProperty(DissolveAmountId))
                        materials[m].SetFloat(DissolveAmountId, dissolveAmount);
                }
            }
        }
    }
}
