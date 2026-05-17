using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Soft UI mask: maskable children fade by the mask graphic's alpha instead of a hard stencil cut.
/// Opacity at mask alpha 0/1 is controlled by <see cref="fadeOpacityMin"/> / <see cref="fadeOpacityMax"/> (0–1).
/// </summary>
[AddComponentMenu("UI/Fade Opacity Mask")]
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class UIFadeOpacityMask : UIBehaviour
{
    static readonly int FadeMaskTexId = Shader.PropertyToID("_FadeMaskTex");
    static readonly int FadeMaskOriginId = Shader.PropertyToID("_FadeMaskOrigin");
    static readonly int FadeMaskAxisXId = Shader.PropertyToID("_FadeMaskAxisX");
    static readonly int FadeMaskAxisYId = Shader.PropertyToID("_FadeMaskAxisY");
    static readonly int FadeOpacityMinId = Shader.PropertyToID("_FadeOpacityMin");
    static readonly int FadeOpacityMaxId = Shader.PropertyToID("_FadeOpacityMax");
    static readonly int FadeEdgeSoftnessId = Shader.PropertyToID("_FadeEdgeSoftness");

    [SerializeField] private Graphic maskGraphic;
    [SerializeField] private bool showMaskGraphic;
    [Tooltip("Child alpha multiplier when the mask sample alpha is 0.")]
    [Range(0f, 1f)] [SerializeField] private float fadeOpacityMin;
    [Tooltip("Child alpha multiplier when the mask sample alpha is 1.")]
    [Range(0f, 1f)] [SerializeField] private float fadeOpacityMax = 1f;
    [Tooltip("0 = hard edge at mid-alpha; 1 = soft ramp across the full mask alpha range.")]
    [Range(0f, 1f)] [SerializeField] private float edgeSoftness = 0.15f;
    [SerializeField] private bool autoAttachMaskableToChildren = true;
    [SerializeField] private Shader maskableShader;

    private readonly List<UIFadeOpacityMaskable> _maskables = new();
    private bool _maskGraphicAlphaStored;
    private float _storedMaskGraphicAlpha = 1f;
    private Vector3 _maskOrigin;
    private Vector3 _maskAxisX;
    private float _maskAxisXLenSq;
    private Vector3 _maskAxisY;
    private float _maskAxisYLenSq;

    public Graphic MaskGraphic => maskGraphic != null ? maskGraphic : maskGraphic = GetComponent<Graphic>();
    public Shader MaskableShader => maskableShader;
    public float FadeOpacityMin => fadeOpacityMin;
    public float FadeOpacityMax => fadeOpacityMax;
    public float EdgeSoftness => edgeSoftness;

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (GetComponent<Mask>() != null)
            Debug.LogWarning(
                $"UIFadeOpacityMask on '{name}': remove Unity's Mask component — it hard-clips via stencil and fights soft fade.",
                this);
        fadeOpacityMin = Mathf.Clamp01(fadeOpacityMin);
        fadeOpacityMax = Mathf.Clamp01(fadeOpacityMax);
        if (fadeOpacityMax < fadeOpacityMin)
            fadeOpacityMax = fadeOpacityMin;
        edgeSoftness = Mathf.Clamp01(edgeSoftness);

        if (maskableShader == null)
            maskableShader = Shader.Find("DiceGame/UI Fade Opacity Maskable");

        if (autoAttachMaskableToChildren)
            SyncChildMaskables();
        SetMaskGraphicVisible(showMaskGraphic);
        MarkMaskDirty();
    }
#endif

    protected override void OnEnable()
    {
        base.OnEnable();
        if (maskableShader == null)
            maskableShader = Shader.Find("DiceGame/UI Fade Opacity Maskable");

        if (maskableShader == null)
            Debug.LogError($"UIFadeOpacityMask on '{name}': shader 'DiceGame/UI Fade Opacity Maskable' not found.", this);

        if (MaskGraphic == null)
            Debug.LogError($"UIFadeOpacityMask on '{name}': assign a Graphic (e.g. Image) for the mask shape.", this);

        if (autoAttachMaskableToChildren)
            SyncChildMaskables();

        SetMaskGraphicVisible(showMaskGraphic);
        MarkMaskDirty();
    }

    protected override void OnDisable()
    {
        SetMaskGraphicVisible(true);
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        SetMaskGraphicVisible(true);
        base.OnDestroy();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        MarkMaskDirty();
    }

    protected override void OnTransformParentChanged()
    {
        if (autoAttachMaskableToChildren)
            SyncChildMaskables();
        MarkMaskDirty();
    }

    private void OnTransformChildrenChanged()
    {
        if (autoAttachMaskableToChildren)
            SyncChildMaskables();
        MarkMaskDirty();
    }

    private void LateUpdate()
    {
        RebuildMaskTransformIfNeeded();
        UIFadeOpacityMaskMaterialCache.ApplyMaskPropertiesToAll(this);
    }

    /// <summary>Opacity multiplier when mask sample alpha is 0.</summary>
    public void SetFadeOpacityMin(float value)
    {
        fadeOpacityMin = Mathf.Clamp01(value);
        MarkMaskDirty();
    }

    /// <summary>Opacity multiplier when mask sample alpha is 1.</summary>
    public void SetFadeOpacityMax(float value)
    {
        fadeOpacityMax = Mathf.Clamp01(value);
        if (fadeOpacityMax < fadeOpacityMin)
            fadeOpacityMin = fadeOpacityMax;
        MarkMaskDirty();
    }

    public void SetEdgeSoftness(float value)
    {
        edgeSoftness = Mathf.Clamp01(value);
        MarkMaskDirty();
    }

    public void SetShowMaskGraphic(bool show)
    {
        showMaskGraphic = show;
        SetMaskGraphicVisible(show);
    }

    internal static UIFadeOpacityMask FindActiveMask(Transform child)
    {
        var masks = child.GetComponentsInParent<UIFadeOpacityMask>(true);
        for (var i = 0; i < masks.Length; i++)
        {
            var m = masks[i];
            if (m != null && m.isActiveAndEnabled)
                return m;
        }

        return null;
    }

    internal void PushMaskProperties(Material mat)
    {
        if (mat == null)
            return;

        RebuildMaskTransformIfNeeded();
        ApplyMaskProperties(mat);
    }

    private void RebuildMaskTransformIfNeeded()
    {
        if (MaskGraphic == null)
            return;

        var rect = MaskGraphic.rectTransform;
        if (rect == null)
            return;

        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        _maskOrigin = corners[0];
        _maskAxisX = corners[3] - corners[0];
        _maskAxisY = corners[1] - corners[0];
        _maskAxisXLenSq = Mathf.Max(_maskAxisX.sqrMagnitude, 1e-8f);
        _maskAxisYLenSq = Mathf.Max(_maskAxisY.sqrMagnitude, 1e-8f);
    }

    private void ApplyMaskProperties(Material mat)
    {
        var tex = MaskGraphic != null ? MaskGraphic.mainTexture : Texture2D.whiteTexture;
        mat.SetTexture(FadeMaskTexId, tex != null ? tex : Texture2D.whiteTexture);
        mat.SetVector(FadeMaskOriginId, new Vector4(_maskOrigin.x, _maskOrigin.y, _maskOrigin.z, 0f));
        mat.SetVector(FadeMaskAxisXId, new Vector4(_maskAxisX.x, _maskAxisX.y, _maskAxisX.z, _maskAxisXLenSq));
        mat.SetVector(FadeMaskAxisYId, new Vector4(_maskAxisY.x, _maskAxisY.y, _maskAxisY.z, _maskAxisYLenSq));
        mat.SetFloat(FadeOpacityMinId, fadeOpacityMin);
        mat.SetFloat(FadeOpacityMaxId, fadeOpacityMax);
        mat.SetFloat(FadeEdgeSoftnessId, edgeSoftness);
    }

    private void MarkMaskDirty()
    {
        RebuildMaskTransformIfNeeded();
        for (var i = 0; i < _maskables.Count; i++)
        {
            var m = _maskables[i];
            if (m != null)
                m.Graphic.SetMaterialDirty();
        }
    }

    private void SyncChildMaskables()
    {
        _maskables.Clear();
        if (MaskGraphic == null)
            return;

        var maskRoot = MaskGraphic.transform;
        var graphics = maskRoot.GetComponentsInChildren<MaskableGraphic>(true);
        for (var i = 0; i < graphics.Length; i++)
        {
            var g = graphics[i];
            if (g == null || g.transform == maskRoot || !g.maskable)
                continue;

            var maskable = g.GetComponent<UIFadeOpacityMaskable>();
            if (maskable == null)
                maskable = g.gameObject.AddComponent<UIFadeOpacityMaskable>();

            _maskables.Add(maskable);
        }
    }

    private void SetMaskGraphicVisible(bool show)
    {
        if (MaskGraphic == null)
            return;

        if (show)
        {
            if (_maskGraphicAlphaStored)
            {
                var c = MaskGraphic.color;
                c.a = _storedMaskGraphicAlpha;
                MaskGraphic.color = c;
                _maskGraphicAlphaStored = false;
            }
        }
        else
        {
            if (!_maskGraphicAlphaStored)
            {
                _storedMaskGraphicAlpha = MaskGraphic.color.a;
                _maskGraphicAlphaStored = true;
            }

            var c = MaskGraphic.color;
            c.a = 0f;
            MaskGraphic.color = c;
        }
    }
}
