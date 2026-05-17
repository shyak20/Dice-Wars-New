using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Optional Play-mode validation for pixel-art post setup.
/// </summary>
[DisallowMultipleComponent]
public sealed class PixelArtPostProcessBootstrap : MonoBehaviour
{
    [SerializeField]
    private bool validateOnPlay = true;

    void Start()
    {
        if (!validateOnPlay)
            return;

        if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urpAsset)
        {
            Debug.LogError(
                "Pixel art: assign a Universal Render Pipeline asset in Project Settings > Graphics.",
                this);
            return;
        }

        if (!TryFindEnabledFeature(urpAsset))
        {
            Debug.LogError(
                "Pixel art feature missing on the URP Renderer. Run Dice Wars > Rendering > Setup Pixel Art Post Process.",
                this);
            return;
        }

        Debug.Log(
            "Pixel art post process is configured. Enter Play mode to preview. " +
            "The feature should run at After Rendering Post Processing on the URP renderer. " +
            "Assign Exclude Layers for crisp in-world UI (Screen Space Camera, not Overlay).",
            this);
    }

    static bool TryFindEnabledFeature(UniversalRenderPipelineAsset urpAsset)
    {
        foreach (ScriptableRendererData rendererData in urpAsset.rendererDataList)
        {
            if (rendererData is not UniversalRendererData universalRenderer)
                continue;

            foreach (ScriptableRendererFeature feature in universalRenderer.rendererFeatures)
            {
                if (feature is PixelArtPostProcessFeature { IsEnabled: true })
                    return true;
            }
        }

        return false;
    }
}
