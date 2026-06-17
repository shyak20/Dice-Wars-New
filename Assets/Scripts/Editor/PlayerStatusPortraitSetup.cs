#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Wires <see cref="PlayerStatus.characterPortraitImage"/> to the masked portrait under Player HP Bar.</summary>
public static class PlayerStatusPortraitSetup
{
    const string MenuPath = "Dice Wars/UI/Wire Player Status Portrait (Fight Scene)";
    const string FightScenePath = "Assets/Scenes/FightScene.unity";

    [MenuItem(MenuPath)]
    public static void WireFightScenePlayerStatusPortrait()
    {
        var scene = EditorSceneManager.OpenScene(FightScenePath, OpenSceneMode.Single);
        var playerStatus = Object.FindObjectOfType<PlayerStatus>(true);
        if (playerStatus == null)
        {
            Debug.LogError("PlayerStatusPortraitSetup: no PlayerStatus in FightScene.");
            return;
        }

        var portraitImage = FindMaskedPortraitImage(playerStatus.transform);
        if (portraitImage == null)
        {
            Debug.LogError(
                "PlayerStatusPortraitSetup: could not find Image at Player HP Bar → Player Image → Player Mask → Player Image.",
                playerStatus);
            return;
        }

        var so = new SerializedObject(playerStatus);
        so.FindProperty("characterPortraitImage").objectReferenceValue = portraitImage;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Player Status portrait image wired.", playerStatus);
    }

    static Image FindMaskedPortraitImage(Transform playerStatusRoot)
    {
        foreach (var image in playerStatusRoot.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name != "Player Image")
                continue;

            var parent = image.transform.parent;
            if (parent != null && parent.name == "Player Mask")
                return image;
        }

        return null;
    }
}
#endif
