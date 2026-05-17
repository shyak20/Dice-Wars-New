#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ensures Blitter is reset before URP recreates the pipeline when this renderer asset is edited.
/// </summary>
[CustomEditor(typeof(UniversalRendererData), true)]
public sealed class UniversalRendererDataEditorGuard : Editor
{
    public override void OnInspectorGUI()
    {
        UniversalRenderPipelineBlitterFix.PrepareForInspectorEdit();
        base.OnInspectorGUI();
    }
}
#endif
