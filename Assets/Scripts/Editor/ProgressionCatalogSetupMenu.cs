#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Creates starter rank/trial assets and a per-character progression catalog.</summary>
public static class ProgressionCatalogSetupMenu
{
    const string DataRoot = "Assets/Data/Progression";
    const string TrialsFolder = DataRoot + "/Trials";
    const string RanksFolder = DataRoot + "/Ranks";

    [MenuItem("Dice Wars/Progression/Create Starter Catalog")]
    public static void CreateStarterCatalog()
    {
        var character = Selection.activeObject as PlayerDataSO;
        if (character == null)
        {
            Debug.LogWarning(
                "ProgressionCatalogSetupMenu: Select a PlayerDataSO in the Project window, then run this menu again.");
            return;
        }

        CreateStarterCatalogForCharacter(character);
    }

    public static void CreateStarterCatalogForCharacter(PlayerDataSO character)
    {
        if (character == null)
            return;

        var catalogPath = $"{DataRoot}/{character.name}_ProgressionCatalog.asset";
        EnsureFolder(DataRoot);
        EnsureFolder(TrialsFolder);
        EnsureFolder(RanksFolder);

        var trialKill = CreateTrial($"{character.name}_Trial_MonstersKilled10",
            "Defeat 10 monsters (includes elites and bosses).", TrialType.MonstersKilled, 10,
            new ProgressionMaxHpReward { amount = 5 });

        var trialGold = CreateTrial($"{character.name}_Trial_CoinsSpend100",
            "Spend 100 coins in shops and unknown events.", TrialType.CoinsSpend, 100,
            new ProgressionStartingGoldReward { amount = 10 });

        var rankUpRewards = new List<ProgressionRewardBase>
        {
            new ProgressionMaxPowerReward { amount = 1 }
        };

        var rankNovice = CreateRank($"{character.name}_Rank_Novice", 0, "Novice",
            "Begin your path against Merlin's curse.",
            new[] { trialKill, trialGold },
            rankUpRewards);

        var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalogSO>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ProgressionCatalogSO>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        catalog.ranks = new List<PlayerRankSO> { rankNovice };
        EditorUtility.SetDirty(catalog);

        character.progressionCatalog = catalog;
        EditorUtility.SetDirty(character);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = catalog;
        Debug.Log($"ProgressionCatalogSetupMenu: created catalog at {catalogPath} for '{character.name}'.");
    }

    static PlayerTrialSO CreateTrial(
        string fileName,
        string description,
        TrialType type,
        int target,
        ProgressionRewardBase completionReward)
    {
        var path = $"{TrialsFolder}/{fileName}.asset";
        var trial = AssetDatabase.LoadAssetAtPath<PlayerTrialSO>(path);
        if (trial == null)
        {
            trial = ScriptableObject.CreateInstance<PlayerTrialSO>();
            AssetDatabase.CreateAsset(trial, path);
        }

        trial.description = description;
        trial.type = type;
        trial.targetValue = target;
        trial.completionRewards = completionReward != null
            ? new List<ProgressionRewardBase> { completionReward }
            : new List<ProgressionRewardBase>();
        trial.SyncTrialIdFromAssetName();
        EditorUtility.SetDirty(trial);
        return trial;
    }

    static PlayerRankSO CreateRank(
        string fileName,
        int index,
        string rankName,
        string flavor,
        PlayerTrialSO[] trials,
        List<ProgressionRewardBase> rankUpRewards)
    {
        var path = $"{RanksFolder}/{fileName}.asset";
        var rank = AssetDatabase.LoadAssetAtPath<PlayerRankSO>(path);
        if (rank == null)
        {
            rank = ScriptableObject.CreateInstance<PlayerRankSO>();
            AssetDatabase.CreateAsset(rank, path);
        }

        rank.rankIndex = index;
        rank.rankName = rankName;
        rank.rankFlavorText = flavor;
        rank.associatedTrials = new List<PlayerTrialSO>(trials);
        rank.rankUpRewards = rankUpRewards ?? new List<ProgressionRewardBase>();
        EditorUtility.SetDirty(rank);
        return rank;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
