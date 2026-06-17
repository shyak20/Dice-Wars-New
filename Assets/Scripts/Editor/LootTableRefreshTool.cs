#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds loot-table ScriptableObject lists from every matching asset in the project.
/// </summary>
public static class LootTableRefreshTool
{
    const string FacesLootTablePath = "Assets/Data/Faces/Faces Loot Table.asset";
    const string CursesLootTablePath = "Assets/Data/Faces/Curses/Curses Loot Table.asset";
    const string AllGemsLootTablePath = "Assets/Data/Gems/All Gems Loot Table.asset";
    const string Level1GemsLootTablePath = "Assets/Data/Gems/Level 1 Gems.asset";
    const string Level2GemsLootTablePath = "Assets/Data/Gems/Level 2 Gems.asset";
    const string Level3GemsLootTablePath = "Assets/Data/Gems/Level 3 Gems.asset";
    const string RelicsLootTablePath = "Assets/Data/Relics/All Relics Loot Table.asset";

    [MenuItem("DiceGame/Loot Tables/Refresh All From Project")]
    public static void RefreshAllFromProject()
    {
        var (faces, curses) = RefreshFaceAndCurseLootTables();
        var allGems = RefreshAllGemsLootTable();
        var level1 = RefreshGemLootTableForLevel(1, Level1GemsLootTablePath);
        var level2 = RefreshGemLootTableForLevel(2, Level2GemsLootTablePath);
        var level3 = RefreshGemLootTableForLevel(3, Level3GemsLootTablePath);
        var relics = RefreshRelicsLootTable();

        AssetDatabase.SaveAssets();

        Debug.Log(
            "LootTableRefreshTool: refreshed loot tables — " +
            $"Faces={faces}, Curses={curses}, AllGems={allGems}, Level1Gems={level1}, Level2Gems={level2}, Level3Gems={level3}, Relics={relics}.");
    }

    static (int faces, int curses) RefreshFaceAndCurseLootTables()
    {
        var allFaces = FindAllAssets<DieFaceSO>();
        var faces = new List<DieFaceSO>();
        var curses = new List<DieFaceSO>();
        for (var i = 0; i < allFaces.Count; i++)
        {
            var face = allFaces[i];
            if (face.type == DieType.Curse)
                curses.Add(face);
            else
                faces.Add(face);
        }

        var faceTable = LoadRequired<FaceLootTableSO>(FacesLootTablePath);
        Undo.RecordObject(faceTable, "Refresh Faces Loot Table");
        faceTable.allPossibleFaces = faces;
        EditorUtility.SetDirty(faceTable);

        var curseTable = LoadOrCreateCurseLootTable();
        Undo.RecordObject(curseTable, "Refresh Curses Loot Table");
        curseTable.allPossibleCurses = curses;
        EditorUtility.SetDirty(curseTable);

        return (faces.Count, curses.Count);
    }

    static CurseLootSO LoadOrCreateCurseLootTable()
    {
        var existing = AssetDatabase.LoadAssetAtPath<CurseLootSO>(CursesLootTablePath);
        if (existing != null)
            return existing;

        EnsureFolder("Assets/Data/Faces/Curses");
        var created = ScriptableObject.CreateInstance<CurseLootSO>();
        created.name = "Curses Loot Table";
        AssetDatabase.CreateAsset(created, CursesLootTablePath);
        return created;
    }

    static int RefreshAllGemsLootTable()
    {
        var table = LoadRequired<GemLootTableSO>(AllGemsLootTablePath);
        var gems = FindAllAssets<GemSO>();
        Undo.RecordObject(table, "Refresh All Gems Loot Table");
        table.allPossibleGems = gems;
        EditorUtility.SetDirty(table);
        return gems.Count;
    }

    static int RefreshGemLootTableForLevel(int level, string assetPath)
    {
        var table = LoadRequired<GemLootTableSO>(assetPath);
        var allGems = FindAllAssets<GemSO>();
        var filtered = new List<GemSO>();
        for (var i = 0; i < allGems.Count; i++)
        {
            var gem = allGems[i];
            if (gem.level == level)
                filtered.Add(gem);
        }

        Undo.RecordObject(table, $"Refresh Level {level} Gems Loot Table");
        table.allPossibleGems = filtered;
        EditorUtility.SetDirty(table);
        return filtered.Count;
    }

    static int RefreshRelicsLootTable()
    {
        var table = LoadRequired<RelicLootTableSO>(RelicsLootTablePath);
        var relics = FindAllAssets<RelicSO>();
        Undo.RecordObject(table, "Refresh Relics Loot Table");
        table.allPossibleRelics = relics;
        EditorUtility.SetDirty(table);
        return relics.Count;
    }

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static T LoadRequired<T>(string assetPath) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
            throw new InvalidOperationException($"LootTableRefreshTool: missing loot table at '{assetPath}'.");
        return asset;
    }

    static List<T> FindAllAssets<T>() where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        var results = new List<T>(guids.Length);
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                results.Add(asset);
        }

        results.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        return results;
    }
}
#endif
