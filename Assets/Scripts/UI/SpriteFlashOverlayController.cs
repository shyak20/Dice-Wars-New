using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives <c>_FlashAmount</c> (0–1) on <see cref="Custom/SpriteFlash Overlay"/> for a UI <see cref="Image"/>.
/// Requires the URP UI variant of that shader (built-in CG does not receive property updates on Canvas).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public sealed class SpriteFlashOverlayController : MonoBehaviour, IMaterialModifier
{
    private const string ExpectedShaderName = "Custom/SpriteFlash Overlay";

    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

    [SerializeField] private Image targetImage;
    [SerializeField, Range(0f, 1f)] private float flashAmount;
    [SerializeField] private Color flashColor = Color.white;

    Material _materialInstance;
    float _lastPushedFlashAmount = -1f;
    Color _lastPushedFlashColor;
    bool _hasPushedFlashColor;

    public Image TargetImage => targetImage;
    public float FlashAmount => flashAmount;

    void OnEnable()
    {
        ResolveTargetImage();
        EnsureMaterialInstance();
        PushFlashPropertiesAndRefresh();
    }

    void OnDisable() => ReleaseMaterialInstance();

    void OnDestroy() => ReleaseMaterialInstance();

    void LateUpdate()
    {
        if (!isActiveAndEnabled || targetImage == null)
            return;

        if (!_hasPushedFlashColor || !Mathf.Approximately(_lastPushedFlashAmount, flashAmount) || _lastPushedFlashColor != flashColor)
            PushFlashPropertiesAndRefresh();
    }

    void OnValidate()
    {
        flashAmount = Mathf.Clamp01(flashAmount);
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage == null)
            return;

        EnsureMaterialInstance();
        PushFlashPropertiesAndRefresh();
    }

    /// <summary>Sets flash strength in [0, 1].</summary>
    public void SetFlashAmount(float amount)
    {
        flashAmount = Mathf.Clamp01(amount);
        PushFlashPropertiesAndRefresh();
    }

    /// <summary>Sets the overlay tint used when flash amount is above zero.</summary>
    public void SetFlashColor(Color color)
    {
        flashColor = color;
        PushFlashPropertiesAndRefresh();
    }

    /// <summary>Rebinds to a different Image (must use SpriteFlash Overlay material).</summary>
    public void SetTargetImage(Image image)
    {
        if (ReferenceEquals(targetImage, image))
            return;

        ReleaseMaterialInstance();
        targetImage = image;
        ResolveTargetImage();
        EnsureMaterialInstance();
        PushFlashPropertiesAndRefresh();
    }

    /// <inheritdoc />
    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (baseMaterial == null || !IsSpriteFlashShader(baseMaterial.shader))
            return baseMaterial;

        ApplyFlashToMaterial(baseMaterial);
        return baseMaterial;
    }

    void ResolveTargetImage()
    {
        if (targetImage != null)
            return;

        targetImage = GetComponent<Image>();
        if (targetImage == null)
            throw new System.InvalidOperationException(
                $"{nameof(SpriteFlashOverlayController)} on '{name}': assign {nameof(targetImage)} or add an {nameof(Image)} on this GameObject.");
    }

    void EnsureMaterialInstance()
    {
        if (targetImage == null)
            return;

        if (_materialInstance != null && targetImage.material == _materialInstance)
            return;

        var current = targetImage.material;
        if (current != null && IsSpriteFlashShader(current.shader))
        {
            _materialInstance = current;
            return;
        }

        if (current == null)
            throw new System.InvalidOperationException(
                $"{nameof(SpriteFlashOverlayController)} on '{name}': assign a material on {nameof(Image)} '{targetImage.name}'.");

        throw new System.InvalidOperationException(
            $"{nameof(SpriteFlashOverlayController)} on '{name}': material on '{targetImage.name}' must use shader '{ExpectedShaderName}' (got '{current.shader.name}').");
    }

    void PushFlashPropertiesAndRefresh()
    {
        if (targetImage == null)
            return;

        EnsureMaterialInstance();

        if (_materialInstance != null)
            ApplyFlashToMaterial(_materialInstance);

        if (targetImage.IsActive() && targetImage.canvas != null)
        {
            var renderingMaterial = targetImage.materialForRendering;
            if (renderingMaterial != null && !ReferenceEquals(renderingMaterial, _materialInstance))
                ApplyFlashToMaterial(renderingMaterial);
        }

        _lastPushedFlashAmount = flashAmount;
        _lastPushedFlashColor = flashColor;
        _hasPushedFlashColor = true;

        targetImage.SetMaterialDirty();
    }

    void ApplyFlashToMaterial(Material material)
    {
        material.SetFloat(FlashAmountId, flashAmount);
        material.SetColor(FlashColorId, flashColor);
    }

    void ReleaseMaterialInstance()
    {
        _materialInstance = null;
        _lastPushedFlashAmount = -1f;
        _hasPushedFlashColor = false;
    }

    static bool IsSpriteFlashShader(Shader shader) =>
        shader != null && shader.name == ExpectedShaderName;
}
