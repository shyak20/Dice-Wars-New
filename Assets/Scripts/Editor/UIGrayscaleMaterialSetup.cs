using UnityEditor;
using UnityEngine;

/// <summary>Ensures <see cref="MaterialPath"/> exists for <c>DiceGame/UI Grayscale</c>.</summary>
public static class UIGrayscaleMaterialSetup
{
    public const string ShaderName = "DiceGame/UI Grayscale";
    public const string ShaderPath = "Assets/Shaders/UI_Grayscale.shader";
    public const string MaterialPath = "Assets/Materials/UI Grayscale.mat";

    [MenuItem("Dice Wars/UI/Create Grayscale Material")]
    public static void CreateOrSelectMaterial() => SelectMaterial(EnsureMaterialAsset(recreate: false));

    [MenuItem("Dice Wars/UI/Recreate Grayscale Material")]
    public static void RecreateMaterial() => SelectMaterial(EnsureMaterialAsset(recreate: true));

    static void SelectMaterial(Material material)
    {
        if (material == null)
            return;

        Selection.activeObject = material;
        EditorGUIUtility.PingObject(material);
    }

    public static Material EnsureMaterialAsset(bool recreate = false)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
            shader = Shader.Find(ShaderName);

        if (shader == null)
        {
            Debug.LogError(
                $"UIGrayscaleMaterialSetup: shader not found at '{ShaderPath}' and Shader.Find('{ShaderName}') failed. " +
                "Fix shader compile errors in the Console first.");
            return null;
        }

        if (recreate && AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            AssetDatabase.DeleteAsset(MaterialPath);

        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
        {
            ApplyDefaults(existing, shader);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        var material = new Material(shader) { name = "UI Grayscale" };
        ApplyDefaults(material, shader);
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"UIGrayscaleMaterialSetup: created material at {MaterialPath}");
        return material;
    }

    static void ApplyDefaults(Material material, Shader shader)
    {
        if (material.shader != shader)
            material.shader = shader;

        material.SetFloat("_GrayscaleAmount", 1f);
        material.SetFloat("_UsePostTint", 0f);
        material.SetColor("_Color", Color.white);
        material.SetColor("_TintColor", Color.white);
        material.SetFloat("_StencilComp", 8f);
        material.SetFloat("_Stencil", 0f);
        material.SetFloat("_StencilOp", 0f);
        material.SetFloat("_StencilWriteMask", 255f);
        material.SetFloat("_StencilReadMask", 255f);
        material.SetFloat("_ColorMask", 15f);
        material.SetFloat("_UseUIAlphaClip", 0f);
        SyncPostTintKeyword(material);
    }

    public static void SyncPostTintKeyword(Material material)
    {
        var useTint = material.GetFloat("_UsePostTint") > 0.5f;
        if (useTint)
            material.EnableKeyword("USE_POST_TINT");
        else
            material.DisableKeyword("USE_POST_TINT");
    }

    class ImportHook : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (var i = 0; i < importedAssets.Length; i++)
            {
                if (!importedAssets[i].EndsWith("UI_Grayscale.shader"))
                    continue;

                EnsureMaterialAsset(recreate: false);
                return;
            }
        }
    }
}
