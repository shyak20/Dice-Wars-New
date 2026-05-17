#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(PixelArtPostProcessFeature))]
public sealed class PixelArtPostProcessFeatureEditor : Editor
{
    public override void OnInspectorGUI()
    {
        UniversalRenderPipelineBlitterFix.PrepareForInspectorEdit();
        base.OnInspectorGUI();
    }
}
#endif
