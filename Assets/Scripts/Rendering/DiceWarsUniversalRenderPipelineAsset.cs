using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP asset that resets Blitter before pipeline creation so repeated
/// <see cref="CreatePipeline"/> calls in the Editor do not throw.
/// </summary>
[CreateAssetMenu(
    fileName = "DiceWars Universal Render Pipeline Asset",
    menuName = "Rendering/Dice Wars/Universal RP Asset (Blitter Safe)")]
public sealed class DiceWarsUniversalRenderPipelineAsset : UniversalRenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline()
    {
#if UNITY_EDITOR
        try
        {
            Blitter.Cleanup();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Blitter cleanup before URP creation failed: {exception.Message}", this);
        }
#endif

        return base.CreatePipeline();
    }
}
