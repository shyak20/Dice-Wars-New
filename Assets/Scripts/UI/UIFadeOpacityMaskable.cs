using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put on the same GameObject as a <see cref="MaskableGraphic"/> (or auto-added by <see cref="UIFadeOpacityMask"/>).
/// Applies the parent <see cref="UIFadeOpacityMask"/> soft alpha fade via <see cref="IMaterialModifier"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MaskableGraphic))]
public sealed class UIFadeOpacityMaskable : MonoBehaviour, IMaterialModifier
{
    [SerializeField] private MaskableGraphic graphic;

    private UIFadeOpacityMask _activeMask;
    private Material _lastBaseMaterial;
    private Material _lastModifiedMaterial;

    public MaskableGraphic Graphic => graphic != null ? graphic : graphic = GetComponent<MaskableGraphic>();

    private void OnEnable()
    {
        Graphic.SetMaterialDirty();
    }

    private void OnDisable()
    {
        ReleaseCachedMaterial();
        Graphic.SetMaterialDirty();
    }

    private void OnDestroy()
    {
        ReleaseCachedMaterial();
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        ReleaseCachedMaterial();

        if (baseMaterial == null || !Graphic.maskable)
            return baseMaterial;

        var mask = UIFadeOpacityMask.FindActiveMask(transform);
        if (mask == null)
            return baseMaterial;

        var shader = mask.MaskableShader;
        if (shader == null)
            return baseMaterial;

        _activeMask = mask;
        _lastBaseMaterial = baseMaterial;
        _lastModifiedMaterial = UIFadeOpacityMaskMaterialCache.Acquire(baseMaterial, mask, shader);
        return _lastModifiedMaterial;
    }

    private void ReleaseCachedMaterial()
    {
        if (_lastModifiedMaterial == null || _lastBaseMaterial == null || _activeMask == null)
        {
            _lastModifiedMaterial = null;
            _lastBaseMaterial = null;
            _activeMask = null;
            return;
        }

        UIFadeOpacityMaskMaterialCache.Release(_lastModifiedMaterial, _lastBaseMaterial, _activeMask);
        _lastModifiedMaterial = null;
        _lastBaseMaterial = null;
        _activeMask = null;
    }
}
