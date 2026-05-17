#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Creates the pixel-art post material and registers <see cref="PixelArtPostProcessFeature"/> on the project URP renderer.
/// </summary>
public static class PixelArtPostProcessSetup
{
    const string RendererAssetPath = "Assets/New Universal Render Pipeline Asset_Renderer.asset";
    const string ShaderName = "Hidden/DiceGame/PixelArtPostProcess";
    const string MaterialPath = "Assets/Materials/PixelArtPostProcess.mat";

    [MenuItem("Dice Wars/Rendering/Setup Pixel Art Post Process")]
    public static void Setup()
    {
        UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
        if (renderer == null)
        {
            Debug.LogError($"Pixel art setup: URP renderer not found at {RendererAssetPath}. Assign the feature manually on your Renderer Data asset.");
            return;
        }

        Material material = EnsureMaterial();
        PixelArtPostProcessFeature feature = FindOrCreateFeature(renderer);
        SerializedObject featureSo = new SerializedObject(feature);
        featureSo.FindProperty("material").objectReferenceValue = material;
        featureSo.FindProperty("isEnabled").boolValue = true;
        featureSo.FindProperty("pixelSize").floatValue = 8f;
        featureSo.FindProperty("renderPassEvent").intValue = (int)PixelArtPostProcessFeature.DefaultPassEvent;
        featureSo.FindProperty("excludeLayers").intValue = 0;
        featureSo.FindProperty("logWhenActive").boolValue = false;
        featureSo.FindProperty("colorLevels").intValue = 0;
        featureSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Pixel art post process is ready. Tune Pixel Size and Exclude Layers on the PixelArtPostProcess feature in " +
            RendererAssetPath + ". Put crisp UI on excluded layers with Screen Space Camera (not Overlay). " +
            "Enter Play mode to preview.");
    }

    static Material EnsureMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
            return existing;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new System.InvalidOperationException($"Shader not found: {ShaderName}. Reimport {ShaderName}.shader.");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var material = new Material(shader) { name = "PixelArtPostProcess" };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    static PixelArtPostProcessFeature FindOrCreateFeature(UniversalRendererData renderer)
    {
        foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
        {
            if (feature is PixelArtPostProcessFeature pixelFeature)
                return pixelFeature;
        }

        var created = ScriptableObject.CreateInstance<PixelArtPostProcessFeature>();
        created.name = "PixelArtPostProcess";
        AssetDatabase.AddObjectToAsset(created, renderer);
        renderer.rendererFeatures.Add(created);
        return created;
    }
}
#endif
