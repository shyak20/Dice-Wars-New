#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Assigns <see cref="PlayerTrialSO.TrialId"/> from the asset file name on import/create.</summary>
public sealed class PlayerTrialSOAssetSync : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (var i = 0; i < importedAssets.Length; i++)
        {
            var trial = AssetDatabase.LoadAssetAtPath<PlayerTrialSO>(importedAssets[i]);
            if (trial == null)
                continue;

            trial.SyncTrialIdFromAssetName();
            EditorUtility.SetDirty(trial);
        }

        for (var i = 0; i < movedAssets.Length; i++)
        {
            var trial = AssetDatabase.LoadAssetAtPath<PlayerTrialSO>(movedAssets[i]);
            if (trial == null)
                continue;

            trial.SyncTrialIdFromAssetName();
            EditorUtility.SetDirty(trial);
        }
    }
}
#endif
