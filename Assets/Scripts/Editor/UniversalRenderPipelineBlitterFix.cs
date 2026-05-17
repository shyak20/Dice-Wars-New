#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Editor helpers for Blitter / URP lifecycle. Does not destroy the active pipeline on project load.
/// </summary>
static class UniversalRenderPipelineBlitterFix
{
    static bool cleanedWhilePipelineMissing;

    static UniversalRenderPipelineBlitterFix()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        EditorApplication.quitting += OnEditorQuitting;
    }

    static void OnEditorQuitting()
    {
        SafeCleanupBlitter();
    }

    static void OnBeforeAssemblyReload()
    {
        SafeCleanupBlitter();
        cleanedWhilePipelineMissing = false;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.ExitingPlayMode:
            case PlayModeStateChange.EnteredEditMode:
                SafeCleanupBlitter();
                cleanedWhilePipelineMissing = false;
                break;
        }
    }

    static void OnEditorUpdate()
    {
        if (GraphicsSettings.defaultRenderPipeline == null)
        {
            cleanedWhilePipelineMissing = false;
            return;
        }

        if (RenderPipelineManager.currentPipeline != null)
        {
            cleanedWhilePipelineMissing = false;
            return;
        }

        if (cleanedWhilePipelineMissing)
            return;

        SafeCleanupBlitter();
        cleanedWhilePipelineMissing = true;
    }

    internal static void PrepareForInspectorEdit()
    {
        Event guiEvent = Event.current;
        if (guiEvent != null
            && (guiEvent.type == EventType.Repaint || guiEvent.type == EventType.Layout))
        {
            return;
        }

        SafeCleanupBlitter();
    }

    internal static void SafeCleanupBlitter()
    {
        try
        {
            Blitter.Cleanup();
        }
        catch (System.Exception exception)
        {
            UnityEngine.Debug.LogWarning($"URP Blitter cleanup failed: {exception.Message}");
        }
    }
}
#endif
