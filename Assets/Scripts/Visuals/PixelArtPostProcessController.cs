using UnityEngine;

/// <summary>
/// Optional scene helper: drives <see cref="PixelArtPostProcessFeature"/> settings at runtime.
/// Run Dice Wars / Rendering / Setup Pixel Art Post Process once in the Editor first.
/// </summary>
[DisallowMultipleComponent]
public sealed class PixelArtPostProcessController : MonoBehaviour
{
    [SerializeField]
    private bool enableEffect = true;

    [SerializeField]
    [Range(1f, 64f)]
    private float pixelSize = 4f;

    [SerializeField]
    [Range(0, 64)]
    private int colorLevels;

    [SerializeField]
    private LayerMask excludeLayers;

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        if (isActiveAndEnabled)
            Apply();
    }

    public void Apply()
    {
        PixelArtPostProcessFeature feature = PixelArtPostProcessFeature.Instance;
        if (feature == null)
            return;

        feature.IsEnabled = enableEffect;
        feature.PixelSize = pixelSize;
        feature.ColorLevels = colorLevels;
        feature.ExcludeLayers = excludeLayers;
    }
}
