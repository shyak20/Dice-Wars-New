#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class ProgressionRewardVisualCatalogSetupMenu
{
    const string DefaultPath = "Assets/Data/ProgressionRewardVisualCatalog.asset";
    const string GameIconIndexPath = "Assets/Data/GameIconIndex.asset";
    const string DiceSelectScenePath = "Assets/Scenes/DiceSelect.unity";
    const string MapScenePath = "Assets/Scenes/MapScene.unity";
    const string CompactRowPrefabPath = "Assets/Prefabs/UI/Tooltips/Trial Reward Row Element.prefab";
    const string FaceRewardDisplayPrefabPath = "Assets/Prefabs/UI/Dice Select/Relic & Gem Display Reward.prefab";

    [MenuItem("DiceGame/Progression/Reward Visual Catalog")]
    public static void CreateRewardVisualCatalog()
    {
        var catalog = EnsureCatalogAsset();
        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
    }

    [MenuItem("DiceGame/Progression/Wire Reward Visual Catalog (Dice Select)")]
    public static void WireDiceSelectScene()
    {
        var catalog = EnsureCatalogAsset();
        var compactRow = AssetDatabase.LoadAssetAtPath<TrialRewardRowElementUI>(CompactRowPrefabPath);
        if (compactRow == null)
        {
            Debug.LogError(
                $"ProgressionRewardVisualCatalogSetupMenu: missing compact row prefab at '{CompactRowPrefabPath}'.");
            return;
        }

        var faceRewardDisplay = AssetDatabase.LoadAssetAtPath<GameObject>(FaceRewardDisplayPrefabPath);

        var changed = WireScene(DiceSelectScenePath, catalog, compactRow, faceRewardDisplay, wirePopups: true);
        changed |= WireScene(MapScenePath, catalog, compactRow, faceRewardDisplay, wirePopups: false);

        if (changed)
            Debug.Log("ProgressionRewardVisualCatalogSetupMenu: wired progression reward visual catalog.");
        else
            Debug.Log("ProgressionRewardVisualCatalogSetupMenu: scenes already wired.");
    }

    static bool WireScene(
        string scenePath,
        ProgressionRewardVisualCatalogSO catalog,
        TrialRewardRowElementUI compactRow,
        GameObject faceRewardDisplay,
        bool wirePopups)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        var changed = false;

        foreach (var manager in Object.FindObjectsOfType<HoverTooltipManager>(true))
        {
            var so = new SerializedObject(manager);
            if (AssignIfDifferent(so, "progressionRewardVisualCatalog", catalog))
                changed = true;
        }

        if (wirePopups)
        {
            foreach (var popup in Object.FindObjectsOfType<ProgressionTrialCompletedPopupView>(true))
            {
                var so = new SerializedObject(popup);
                if (AssignIfDifferent(so, "rewardVisualCatalog", catalog))
                    changed = true;
                if (faceRewardDisplay != null
                    && AssignIfDifferent(so, "faceRewardDisplayPrefab", faceRewardDisplay))
                    changed = true;
            }

            foreach (var popup in Object.FindObjectsOfType<ProgressionRankUpPopupView>(true))
            {
                var so = new SerializedObject(popup);
                if (AssignIfDifferent(so, "rewardVisualCatalog", catalog))
                    changed = true;
                if (AssignIfDifferent(so, "compactRewardRowPrefab", compactRow))
                    changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        EditorSceneManager.CloseScene(scene, true);
        return changed;
    }

    static ProgressionRewardVisualCatalogSO EnsureCatalogAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<ProgressionRewardVisualCatalogSO>(DefaultPath);
        if (existing != null)
            return existing;

        var catalog = ScriptableObject.CreateInstance<ProgressionRewardVisualCatalogSO>();
        var iconIndex = AssetDatabase.LoadAssetAtPath<GameIconIndexSO>(GameIconIndexPath);
        if (iconIndex != null)
        {
            var so = new SerializedObject(catalog);
            so.FindProperty("iconIndex").objectReferenceValue = iconIndex;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var directory = Path.GetDirectoryName(DefaultPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        AssetDatabase.CreateAsset(catalog, DefaultPath);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    static bool AssignIfDifferent(SerializedObject so, string propertyName, Object value)
    {
        var property = so.FindProperty(propertyName);
        if (property == null)
            return false;

        if (property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
#endif
