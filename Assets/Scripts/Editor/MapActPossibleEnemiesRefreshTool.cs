#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds each <see cref="MapActDefinitionSO.possibleEnemies"/> list from <see cref="EnemyTypeSO"/>
/// assets under <c>Assets/Data/Enemies/Act N/</c>. Spawn adds referenced by
/// <see cref="EnemyTypeSO.spawnOnLoadAdds"/> are excluded.
/// </summary>
public static class MapActPossibleEnemiesRefreshTool
{
    const string EnemiesRoot = "Assets/Data/Enemies";
    const string ActMapsFolder = "Assets/Data/Run";
    const string ActMapPathFormat = "Assets/Data/Run/Act {0} Map.asset";

    static readonly Regex ActFolderRegex = new Regex(@"(?:^|/)Act\s+(\d+)(?:/|$)", RegexOptions.CultureInvariant);

    [MenuItem("DiceGame/Map/Refresh Act Possible Enemies From Folders")]
    public static void RefreshActPossibleEnemiesFromFolders()
    {
        var allEnemies = FindAllAssets<EnemyTypeSO>(EnemiesRoot);
        var spawnAdds = CollectSpawnOnLoadAdds(allEnemies);

        var enemiesByAct = new Dictionary<int, List<EnemyTypeSO>>();
        var skippedNoActFolder = new List<string>();
        var skippedSpawnAdds = new List<string>();

        for (var i = 0; i < allEnemies.Count; i++)
        {
            var enemy = allEnemies[i];
            if (enemy == null)
                continue;

            var path = AssetDatabase.GetAssetPath(enemy);
            if (spawnAdds.Contains(enemy))
            {
                skippedSpawnAdds.Add($"{enemy.name} ({path})");
                continue;
            }

            if (!TryGetActIndexFromAssetPath(path, out var actIndex))
            {
                skippedNoActFolder.Add($"{enemy.name} ({path})");
                continue;
            }

            if (!enemiesByAct.TryGetValue(actIndex, out var list))
            {
                list = new List<EnemyTypeSO>();
                enemiesByAct[actIndex] = list;
            }

            list.Add(enemy);
        }

        var actIndices = DiscoverActIndices(enemiesByAct);
        var report = new StringBuilder();
        report.AppendLine("MapActPossibleEnemiesRefreshTool:");

        var updatedActs = 0;
        foreach (var actIndex in actIndices)
        {
            var mapPath = string.Format(ActMapPathFormat, actIndex);
            var map = AssetDatabase.LoadAssetAtPath<MapActDefinitionSO>(mapPath);
            if (map == null)
            {
                report.AppendLine($"  Act {actIndex}: skipped — missing map asset at '{mapPath}'.");
                continue;
            }

            enemiesByAct.TryGetValue(actIndex, out var enemiesForAct);
            if (enemiesForAct == null)
                enemiesForAct = new List<EnemyTypeSO>();

            enemiesForAct.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

            Undo.RecordObject(map, "Refresh Act Possible Enemies");
            map.possibleEnemies = new List<EnemyTypeSO>(enemiesForAct);
            EditorUtility.SetDirty(map);
            updatedActs++;

            report.AppendLine($"  Act {actIndex}: {enemiesForAct.Count} possibleEnemies → '{mapPath}'.");
        }

        AssetDatabase.SaveAssets();

        if (skippedSpawnAdds.Count > 0)
        {
            report.AppendLine($"  Excluded {skippedSpawnAdds.Count} spawn-on-load add(s):");
            for (var i = 0; i < skippedSpawnAdds.Count; i++)
                report.AppendLine($"    - {skippedSpawnAdds[i]}");
        }

        if (skippedNoActFolder.Count > 0)
        {
            report.AppendLine($"  Skipped {skippedNoActFolder.Count} enemy asset(s) not under an Act folder:");
            for (var i = 0; i < skippedNoActFolder.Count; i++)
                report.AppendLine($"    - {skippedNoActFolder[i]}");
        }

        report.AppendLine($"  Updated {updatedActs} act map asset(s).");
        Debug.Log(report.ToString());
    }

    static HashSet<EnemyTypeSO> CollectSpawnOnLoadAdds(IReadOnlyList<EnemyTypeSO> allEnemies)
    {
        var spawnAdds = new HashSet<EnemyTypeSO>();
        for (var i = 0; i < allEnemies.Count; i++)
        {
            var enemy = allEnemies[i];
            if (enemy?.spawnOnLoadAdds == null)
                continue;

            for (var j = 0; j < enemy.spawnOnLoadAdds.Count; j++)
            {
                var add = enemy.spawnOnLoadAdds[j];
                if (add != null)
                    spawnAdds.Add(add);
            }
        }

        return spawnAdds;
    }

    static List<int> DiscoverActIndices(Dictionary<int, List<EnemyTypeSO>> enemiesByAct)
    {
        var acts = new HashSet<int>(enemiesByAct.Keys);

        var mapGuids = AssetDatabase.FindAssets("t:MapActDefinitionSO", new[] { ActMapsFolder });
        for (var i = 0; i < mapGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(mapGuids[i]);
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (TryParseActIndexFromMapName(fileName, out var actIndex))
                acts.Add(actIndex);
        }

        var sorted = new List<int>(acts);
        sorted.Sort();
        return sorted;
    }

    static bool TryGetActIndexFromAssetPath(string assetPath, out int actIndex)
    {
        actIndex = 0;
        if (string.IsNullOrEmpty(assetPath))
            return false;

        var normalized = assetPath.Replace('\\', '/');
        if (!normalized.StartsWith(EnemiesRoot + "/", StringComparison.Ordinal))
            return false;

        var match = ActFolderRegex.Match(normalized);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out actIndex) || actIndex <= 0)
            return false;

        return true;
    }

    static bool TryParseActIndexFromMapName(string mapAssetName, out int actIndex)
    {
        actIndex = 0;
        if (string.IsNullOrWhiteSpace(mapAssetName))
            return false;

        var match = Regex.Match(mapAssetName, @"^Act\s+(\d+)\s+Map$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out actIndex) || actIndex <= 0)
            return false;

        return true;
    }

    static List<T> FindAllAssets<T>(string folder) where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        var results = new List<T>(guids.Length);
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                results.Add(asset);
        }

        return results;
    }
}
#endif
