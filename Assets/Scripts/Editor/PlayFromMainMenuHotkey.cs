using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlayFromMainMenuHotkey
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    // Ctrl+G (Windows) / Cmd+G (macOS)
    [MenuItem("Tools/Dice Wars/Play From Main Menu %g")]
    private static void PlayFromMainMenu()
    {
        if (EditorApplication.isPlaying)
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(MainMenuScenePath);
        if (!scene.IsValid())
        {
            UnityEngine.Debug.LogError($"PlayFromMainMenuHotkey: Failed to open scene at '{MainMenuScenePath}'.");
            return;
        }

        EditorApplication.isPlaying = true;
    }
}
