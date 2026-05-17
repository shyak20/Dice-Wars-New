using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pixelates the camera color target after post-processing and before the final blit to the screen.
/// Screen Space Overlay UI drawn afterward stays sharp. Use exclude layers + Screen Space Camera UI for crisp in-world UI.
/// </summary>
public sealed class PixelArtPostProcessFeature : ScriptableRendererFeature
{
    internal static readonly int PixelSizeShaderId = Shader.PropertyToID("_PixelSize");
    internal static readonly int ColorLevelsShaderId = Shader.PropertyToID("_ColorLevels");

    // Must run while cameraColor is still a real render-graph texture (before final blit to the back buffer).
    public const RenderPassEvent DefaultPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    const int ShaderPassPixelate = 0;

    [SerializeField]
    private bool isEnabled = true;

    [SerializeField]
    private Material material;

    [SerializeField]
    private RenderPassEvent renderPassEvent = DefaultPassEvent;

    [SerializeField]
    [Range(1f, 64f)]
    private float pixelSize = 8f;

    [SerializeField]
    [Range(0, 64)]
    private int colorLevels;

    [SerializeField]
    private bool affectSceneView;

    [SerializeField]
    [Tooltip("Objects on these layers are drawn sharp on top after pixelation.")]
    private LayerMask excludeLayers;

    [SerializeField]
    [Tooltip("Log once when the effect runs successfully in Play mode.")]
    private bool logWhenActive;

    PixelArtPostProcessPass cameraPass;
    bool loggedActive;

    public static PixelArtPostProcessFeature Instance { get; private set; }

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public float PixelSize
    {
        get => pixelSize;
        set => pixelSize = Mathf.Max(1f, value);
    }

    public int ColorLevels
    {
        get => colorLevels;
        set => colorLevels = Mathf.Max(0, value);
    }

    public LayerMask ExcludeLayers
    {
        get => excludeLayers;
        set => excludeLayers = value;
    }

    public Material Material => material;

    public override void Create()
    {
        Instance = this;
        cameraPass ??= new PixelArtPostProcessPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!isEnabled || material == null || cameraPass == null)
            return;

        CameraType cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            return;

        if (cameraType == CameraType.SceneView && !affectSceneView)
            return;

        if (!ShouldRunCameraPass(renderingData.cameraData))
            return;

        cameraPass.ConfigureSettings(material, renderPassEvent, pixelSize, colorLevels, excludeLayers, logWhenActive);
        renderer.EnqueuePass(cameraPass);
    }

#if URP_COMPATIBILITY_MODE
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (!isEnabled || material == null || cameraPass == null)
            return;

        if (!ShouldRunCameraPass(renderingData.cameraData))
            return;

        cameraPass.ConfigureInput(ScriptableRenderPassInput.Color);
        cameraPass.SetTarget(renderer.cameraColorTargetHandle);
    }
#endif

    protected override void Dispose(bool disposing)
    {
        cameraPass?.Dispose();
    }

    static bool ShouldRunCameraPass(CameraData cameraData)
    {
        if (cameraData.resolveFinalTarget)
            return true;

        CameraRenderType renderType = cameraData.renderType;
        if (renderType == CameraRenderType.Overlay)
            return true;

        if (renderType == CameraRenderType.Base)
        {
            UniversalAdditionalCameraData additional = cameraData.camera.GetUniversalAdditionalCameraData();
            if (additional != null && additional.cameraStack.Count == 0)
                return true;
        }

        return false;
    }

    sealed class PixelArtPostProcessPass : ScriptableRenderPass
    {
        static readonly ProfilingSampler ProfilingSampler = new("PixelArtPostProcess");

        const string TempTextureName = "_PixelArtPostProcessTemp";
        const string PixelatePassName = "PixelArtPostProcessPixelate";
        const string CopyBackPassName = "PixelArtPostProcessCopyBack";
        const string ExcludeOpaquePassName = "PixelArtPostProcessExcludeOpaque";
        const string ExcludeTransparentPassName = "PixelArtPostProcessExcludeTransparent";

        Material material;
        float pixelSize = 8f;
        int colorLevels;
        LayerMask excludeLayers;
        bool logWhenActive;
        RTHandle cameraColorTarget;
        RTHandle tempColor;
        List<ShaderTagId> shaderTags;

        public void ConfigureSettings(
            Material mat,
            RenderPassEvent passEvent,
            float pixels,
            int levels,
            LayerMask excluded,
            bool logOnce)
        {
            material = mat;
            renderPassEvent = passEvent;
            pixelSize = pixels;
            colorLevels = levels;
            excludeLayers = excluded;
            logWhenActive = logOnce;
        }

        public void SetTarget(RTHandle colorTarget)
        {
            cameraColorTarget = colorTarget;
        }

#if URP_COMPATIBILITY_MODE
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (cameraColorTarget == null)
                return;

            ConfigureTarget(cameraColorTarget);
            EnsureTempColor(renderingData.cameraData.cameraTargetDescriptor);
        }
#endif

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

            ApplyMaterialProperties();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogWarning(
                    "Pixel art post process: Render Pass Event is too late (active target is the system back buffer). " +
                    $"Set it to {nameof(RenderPassEvent.AfterRenderingPostProcessing)} or earlier on the PixelArtPostProcess renderer feature.",
                    PixelArtPostProcessFeature.Instance);
                return;
            }

            RecordRenderGraphCameraColor(renderGraph, resourceData, cameraData, renderingData);
        }

        void RecordRenderGraphCameraColor(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            UniversalCameraData cameraData,
            UniversalRenderingData renderingData)
        {
            TextureHandle cameraColor = resourceData.cameraColor;
            if (!cameraColor.IsValid())
                cameraColor = resourceData.activeColorTexture;

            if (!cameraColor.IsValid())
                return;

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;

            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                TempTextureName,
                false);

            if (!temp.IsValid())
                return;

            var pixelateBlit = new RenderGraphUtils.BlitMaterialParameters(
                cameraColor,
                temp,
                material,
                ShaderPassPixelate);
            renderGraph.AddBlitPass(pixelateBlit, PixelatePassName);

            if (renderGraph.CanAddCopyPass(temp, cameraColor))
                renderGraph.AddCopyPass(temp, cameraColor, CopyBackPassName);
            else
                renderGraph.AddBlitPass(temp, cameraColor, Vector2.one, Vector2.zero, passName: CopyBackPassName);

            RecordExcludedLayerPasses(renderGraph, resourceData, cameraData, renderingData, cameraColor);
            TryLogActive(cameraData.camera);
        }

        void EnsureTempColor(RenderTextureDescriptor cameraTargetDescriptor)
        {
            if (cameraColorTarget == null)
                return;

            RenderTextureDescriptor desc = cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(
                ref tempColor,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "_PixelArtPostProcessTemp");
        }

#if URP_COMPATIBILITY_MODE
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || cameraColorTarget == null || tempColor == null)
                return;

            ApplyMaterialProperties();

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, tempColor, material, ShaderPassPixelate);
                Blitter.BlitCameraTexture(cmd, tempColor, cameraColorTarget);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);

            DrawExcludedLayers(context, ref renderingData);
            TryLogActive(renderingData.cameraData.camera);
        }
#endif

        void ApplyMaterialProperties()
        {
            material.SetFloat(PixelSizeShaderId, Mathf.Max(1f, pixelSize));
            material.SetFloat(ColorLevelsShaderId, colorLevels);
        }

        void TryLogActive(Camera camera)
        {
            PixelArtPostProcessFeature feature = PixelArtPostProcessFeature.Instance;
            if (feature == null || !logWhenActive || feature.loggedActive)
                return;

            feature.loggedActive = true;
            Debug.Log(
                $"Pixel art post process applied ({camera.name}, event {renderPassEvent}, pixel size {pixelSize}).",
                feature);
        }

        void RecordExcludedLayerPasses(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            UniversalCameraData cameraData,
            UniversalRenderingData renderingData,
            TextureHandle colorTarget)
        {
            if (excludeLayers == 0)
                return;

            RecordExcludedLayerPass(
                renderGraph,
                resourceData,
                cameraData,
                renderingData,
                colorTarget,
                ExcludeOpaquePassName,
                SortingCriteria.CommonOpaque,
                RenderQueueRange.opaque);

            RecordExcludedLayerPass(
                renderGraph,
                resourceData,
                cameraData,
                renderingData,
                colorTarget,
                ExcludeTransparentPassName,
                SortingCriteria.CommonTransparent,
                RenderQueueRange.transparent);
        }

        void RecordExcludedLayerPass(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            UniversalCameraData cameraData,
            UniversalRenderingData renderingData,
            TextureHandle colorTarget,
            string passName,
            SortingCriteria sortingCriteria,
            RenderQueueRange queueRange)
        {
            DrawingSettings drawingSettings = CreateExcludeDrawingSettings(
                cameraData,
                renderingData,
                sortingCriteria);
            FilteringSettings filteringSettings = new FilteringSettings(queueRange, excludeLayers);
            RendererListParams rendererListParams = new RendererListParams(
                renderingData.cullResults,
                drawingSettings,
                filteringSettings);
            RendererListHandle rendererList = renderGraph.CreateRendererList(rendererListParams);

            using var builder = renderGraph.AddRasterRenderPass<DrawPassData>(passName, out DrawPassData passData);
            passData.rendererList = rendererList;
            builder.UseRendererList(rendererList);
            builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
            if (resourceData.activeDepthTexture.IsValid())
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

            builder.SetRenderFunc(static (DrawPassData data, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(data.rendererList);
            });
        }

        DrawingSettings CreateExcludeDrawingSettings(
            UniversalCameraData cameraData,
            UniversalRenderingData renderingData,
            SortingCriteria sortingCriteria)
        {
            EnsureShaderTags();

            SortingSettings sortingSettings = new SortingSettings(cameraData.camera)
            {
                criteria = sortingCriteria,
            };

            DrawingSettings drawingSettings = new DrawingSettings(shaderTags[0], sortingSettings)
            {
                perObjectData = renderingData.perObjectData,
                enableDynamicBatching = renderingData.supportsDynamicBatching,
            };

            for (int i = 1; i < shaderTags.Count; i++)
                drawingSettings.SetShaderPassName(i, shaderTags[i]);

            return drawingSettings;
        }

        void DrawExcludedLayers(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (excludeLayers == 0)
                return;

            DrawExcludedLayers(context, ref renderingData, SortingCriteria.CommonOpaque, RenderQueueRange.opaque);
            DrawExcludedLayers(context, ref renderingData, SortingCriteria.CommonTransparent, RenderQueueRange.transparent);
        }

        void DrawExcludedLayers(
            ScriptableRenderContext context,
            ref RenderingData renderingData,
            SortingCriteria sortingCriteria,
            RenderQueueRange queueRange)
        {
            EnsureShaderTags();

            DrawingSettings drawingSettings = CreateDrawingSettings(shaderTags[0], ref renderingData, sortingCriteria);
            for (int i = 1; i < shaderTags.Count; i++)
                drawingSettings.SetShaderPassName(i, shaderTags[i]);

            FilteringSettings filteringSettings = new FilteringSettings(queueRange, excludeLayers);
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
        }

        void EnsureShaderTags()
        {
            if (shaderTags != null)
                return;

            shaderTags = new List<ShaderTagId>(4)
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("Universal2D"),
            };
        }

        public void Dispose()
        {
            tempColor?.Release();
        }

        sealed class DrawPassData
        {
            public RendererListHandle rendererList;
        }
    }
}
