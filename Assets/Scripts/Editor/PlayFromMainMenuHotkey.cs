using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayFromMainMenuHotkey
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    // Ctrl+G (Windows) / Cmd+G (macOS)
    [MenuItem("Tools/Dice Wars/Play From Main Menu %g")]
    private static void PlayFromMainMenu() => PlayFromMainMenuInternal(clearPlayerPrefs: false);

    // Ctrl+H (Windows) / Cmd+H (macOS) — same as above, then clears all PlayerPrefs before play.
    [MenuItem("Tools/Dice Wars/Play From Main Menu (Clear PlayerPrefs) %h")]
    private static void PlayFromMainMenuClearPlayerPrefs() => PlayFromMainMenuInternal(clearPlayerPrefs: true);

    private static void PlayFromMainMenuInternal(bool clearPlayerPrefs)
    {
        if (EditorApplication.isPlaying)
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (clearPlayerPrefs)
            PlayerSaveReset.DeleteAllPlayerPrefsAndProgression();

        var scene = EditorSceneManager.OpenScene(MainMenuScenePath);
        if (!scene.IsValid())
        {
            Debug.LogError($"PlayFromMainMenuHotkey: Failed to open scene at '{MainMenuScenePath}'.");
            return;
        }

        EditorApplication.isPlaying = true;
    }
}
