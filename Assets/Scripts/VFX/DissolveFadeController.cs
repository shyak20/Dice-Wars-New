using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades RealToon <c>Cutout</c> (<c>_Cutout</c>) from 0 (visible) to 1 (hidden) on die renderers.
/// Disabled on the dice prefab; enabled only when dissolving after a roll. Face materials must be assigned first
/// (see <see cref="DieVisualizer.Initialize"/>). Active face effect icon quads fade from their current cutout.
/// </summary>
[DisallowMultipleComponent]
public sealed class DissolveFadeController : MonoBehaviour
{
    public const string RealToonCutoutPropertyName = "_Cutout";
    public const string RealToonCutoutKeyword = "N_F_CO_ON";

    private static readonly int CutoutId = Shader.PropertyToID(RealToonCutoutPropertyName);
    private static readonly int CutoutFeatureToggleId = Shader.PropertyToID("_N_F_CO");

    struct CutoutFadeSlot
    {
        public Material Material;
        public float StartCutout;
        public bool IsDieBody;
    }

    [Tooltip("Renderers to fade. If empty, uses the DieVisualizer mesh renderer plus active face effect icon quads.")]
    [SerializeField] private Renderer[] renderers;

    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Fade")]
    [Tooltip("Seconds to reach the target cutout when fading. 0 = instant.")]
    [SerializeField, Min(0f)] private float fadeDurationSeconds = 1f;

    [Tooltip("On Start, fades from fully visible (0) to Cutout below.")]
    [SerializeField] private bool animateToCutoutOnStart;

    [SerializeField, Range(0f, 1f)] private float cutoutAmount;

    Renderer _dieBodyRenderer;
    CutoutFadeSlot[] _fadeSlots;
    float _fadeTargetCutout = 1f;
    float _fadeStartCutout;
    Coroutine _fadeRoutine;

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

        RebuildFadeSlots();
        ApplyCutoutAmount();
    }

    private void Start()
    {
        if (!isActiveAndEnabled || !animateToCutoutOnStart)
            return;

        var target = cutoutAmount;
        cutoutAmount = 0f;
        RebuildFadeSlots();
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

        _fadeTargetCutout = Mathf.Clamp01(targetAmount);
        _fadeStartCutout = cutoutAmount;
        RefreshIconFadeStarts();

        if (durationSeconds <= 0f)
        {
            CutoutAmount = _fadeTargetCutout;
            return;
        }

        _fadeRoutine = StartCoroutine(CoFadeTo(_fadeTargetCutout, durationSeconds));
    }

    public void StopFade()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }

    /// <summary>Die body immediately visible (cutout 0). Icon quads keep their current cutout.</summary>
    public void ShowImmediate()
    {
        StopFade();
        cutoutAmount = 0f;
        _fadeStartCutout = 0f;
        _fadeTargetCutout = 1f;
        ApplyDieBodyCutout(0f);
        SyncDieBodyFadeStarts(0f);
        ApplyCutoutAmount();
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
        _fadeStartCutout = start;
        var elapsed = 0f;
        target = Mathf.Clamp01(target);
        _fadeTargetCutout = target;

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
        _fadeSlots = null;
        CacheRenderers();
        if (!isActiveAndEnabled)
            return;

        ValidateRealToonMaterials();
        RebuildFadeSlots();
        ApplyCutoutAmount();
    }

    private void CacheRenderers()
    {
        if (renderers != null && renderers.Length > 0)
            return;

        var visualizer = GetComponent<DieVisualizer>();
        if (visualizer != null && visualizer.meshRenderer != null)
        {
            _dieBodyRenderer = visualizer.meshRenderer;
            var combined = new List<Renderer> { _dieBodyRenderer };
            visualizer.AppendActiveFaceEffectIconRenderers(combined);
            renderers = combined.ToArray();
            return;
        }

        _dieBodyRenderer = null;
        renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
    }

    private void RebuildFadeSlots()
    {
        if (renderers == null || renderers.Length == 0)
        {
            _fadeSlots = null;
            return;
        }

        var slots = new List<CutoutFadeSlot>();
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var materials = renderer.materials;
            var isDieBody = _dieBodyRenderer != null && renderer == _dieBodyRenderer;
            for (var m = 0; m < materials.Length; m++)
            {
                var mat = materials[m];
                if (mat == null || !mat.HasProperty(CutoutId))
                    continue;

                slots.Add(new CutoutFadeSlot
                {
                    Material = mat,
                    StartCutout = mat.GetFloat(CutoutId),
                    IsDieBody = isDieBody
                });
            }
        }

        _fadeSlots = slots.ToArray();
    }

    void RefreshIconFadeStarts()
    {
        if (_fadeSlots == null || _fadeSlots.Length == 0)
            RebuildFadeSlots();

        if (_fadeSlots == null)
            return;

        for (var i = 0; i < _fadeSlots.Length; i++)
        {
            if (_fadeSlots[i].IsDieBody)
                continue;

            var mat = _fadeSlots[i].Material;
            if (mat == null)
                continue;

            var slot = _fadeSlots[i];
            slot.StartCutout = mat.GetFloat(CutoutId);
            _fadeSlots[i] = slot;
        }
    }

    void SyncDieBodyFadeStarts(float cutout)
    {
        if (_fadeSlots == null)
            RebuildFadeSlots();

        if (_fadeSlots == null)
            return;

        for (var i = 0; i < _fadeSlots.Length; i++)
        {
            if (!_fadeSlots[i].IsDieBody)
                continue;

            var slot = _fadeSlots[i];
            slot.StartCutout = cutout;
            _fadeSlots[i] = slot;
        }
    }

    void ApplyDieBodyCutout(float cutout)
    {
        if (_dieBodyRenderer == null)
            return;

        var materials = _dieBodyRenderer.materials;
        for (var m = 0; m < materials.Length; m++)
        {
            var mat = materials[m];
            if (mat == null || !mat.HasProperty(CutoutId))
                continue;

            EnableRealToonCutout(mat);
            mat.SetFloat(CutoutId, cutout);
        }
    }

    private static void EnableRealToonCutout(Material material)
    {
        material.EnableKeyword(RealToonCutoutKeyword);
        if (material.HasProperty(CutoutFeatureToggleId))
            material.SetFloat(CutoutFeatureToggleId, 1f);
    }

    private void ApplyCutoutAmount()
    {
        if (!isActiveAndEnabled)
            return;

        if (_fadeSlots == null || _fadeSlots.Length == 0)
            RebuildFadeSlots();

        if (_fadeSlots == null || _fadeSlots.Length == 0)
            return;

        var progress = ComputeFadeProgress();

        for (var i = 0; i < _fadeSlots.Length; i++)
        {
            var slot = _fadeSlots[i];
            var mat = slot.Material;
            if (mat == null || !mat.HasProperty(CutoutId))
                continue;

            var value = slot.IsDieBody
                ? cutoutAmount
                : Mathf.Lerp(slot.StartCutout, _fadeTargetCutout, progress);

            EnableRealToonCutout(mat);
            mat.SetFloat(CutoutId, value);
        }
    }

    float ComputeFadeProgress()
    {
        if (Mathf.Approximately(_fadeStartCutout, _fadeTargetCutout))
            return Mathf.Approximately(cutoutAmount, _fadeTargetCutout) ? 1f : 0f;

        return Mathf.Clamp01(Mathf.InverseLerp(_fadeStartCutout, _fadeTargetCutout, cutoutAmount));
    }
}
