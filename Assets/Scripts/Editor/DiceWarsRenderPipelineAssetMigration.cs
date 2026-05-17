#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-time migration of the project's URP asset to <see cref="DiceWarsUniversalRenderPipelineAsset"/>.
/// </summary>
[InitializeOnLoad]
static class DiceWarsRenderPipelineAssetMigration
{
    const string SessionKey = "DiceWars.BlitterSafeUrpMigrated";
    const string DefaultUrpAssetPath = "Assets/New Universal Render Pipeline Asset.asset";

    static DiceWarsRenderPipelineAssetMigration()
    {
        EditorApplication.delayCall += TryMigrateOnProjectLoad;
    }

    [MenuItem("Dice Wars/Rendering/Migrate URP Asset (Fix Blitter Errors)")]
    public static void MigrateFromMenu()
    {
        if (MigrateAssetAtPath(DefaultUrpAssetPath))
        {
            Debug.Log(
                "URP asset migrated to DiceWarsUniversalRenderPipelineAsset. " +
                "Blitter errors on project load should stop.");
        }
    }

    static void TryMigrateOnProjectLoad()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (AssetDatabase.LoadAssetAtPath<DiceWarsUniversalRenderPipelineAsset>(DefaultUrpAssetPath) != null)
        {
            SessionState.SetBool(SessionKey, true);
            return;
        }

        RenderPipelineAsset current = GraphicsSettings.defaultRenderPipeline;
        if (current is DiceWarsUniversalRenderPipelineAsset)
        {
            SessionState.SetBool(SessionKey, true);
            return;
        }

        if (current is not UniversalRenderPipelineAsset)
            return;

        string path = AssetDatabase.GetAssetPath(current);
        if (string.IsNullOrEmpty(path))
            path = DefaultUrpAssetPath;

        if (MigrateAssetAtPath(path))
        {
            SessionState.SetBool(SessionKey, true);
            DiceWarsUniversalRenderPipelineAsset migrated =
                AssetDatabase.LoadAssetAtPath<DiceWarsUniversalRenderPipelineAsset>(path);
            Debug.Log(
                "Automatically migrated the URP asset to DiceWarsUniversalRenderPipelineAsset to prevent Blitter initialization errors.",
                migrated);
        }
    }

    static bool MigrateAssetAtPath(string assetPath)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (asset == null)
        {
            Debug.LogError($"URP asset not found at {assetPath}. Assign Graphics Settings manually.");
            return false;
        }

        if (asset is DiceWarsUniversalRenderPipelineAsset)
            return true;

        MonoScript newScript = FindMonoScript<DiceWarsUniversalRenderPipelineAsset>();
        if (newScript == null)
        {
            Debug.LogError("DiceWarsUniversalRenderPipelineAsset script is missing. Wait for compilation to finish.");
            return false;
        }

        bool wasAssignedInGraphicsSettings = IsGraphicsSettingsPipelineAtPath(assetPath);

        SerializedObject serializedAsset = new SerializedObject(asset);
        SerializedProperty scriptProperty = serializedAsset.FindProperty("m_Script");
        if (scriptProperty == null)
        {
            Debug.LogError("Could not retarget URP asset script.");
            return false;
        }

        scriptProperty.objectReferenceValue = newScript;
        serializedAsset.ApplyModifiedPropertiesWithoutUndo();

        // Retargeting m_Script destroys the old managed wrapper; only touch the asset by path after this.
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        DiceWarsUniversalRenderPipelineAsset migrated =
            AssetDatabase.LoadAssetAtPath<DiceWarsUniversalRenderPipelineAsset>(assetPath);
        if (migrated == null)
        {
            Debug.LogError($"URP asset migration failed for {assetPath}. Use the menu item again after scripts compile.");
            return false;
        }

        EditorUtility.SetDirty(migrated);

        if (wasAssignedInGraphicsSettings)
            GraphicsSettings.defaultRenderPipeline = migrated;

        return true;
    }

    static bool IsGraphicsSettingsPipelineAtPath(string assetPath)
    {
        RenderPipelineAsset assigned = GraphicsSettings.defaultRenderPipeline;
        if (assigned == null)
            return assetPath == DefaultUrpAssetPath;

        string assignedPath = AssetDatabase.GetAssetPath(assigned);
        return assignedPath == assetPath;
    }

    static MonoScript FindMonoScript<T>() where T : ScriptableObject
    {
        string typeName = typeof(T).Name;
        string[] guids = AssetDatabase.FindAssets($"t:MonoScript {typeName}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == typeof(T))
                return script;
        }

        return null;
    }
}
#endif
