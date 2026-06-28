using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

static class AnimatorStateRandomStartToggleDrawer
{
    public static void Draw(AnimatorState state)
    {
        if (state == null)
        {
            EditorGUILayout.HelpBox("Select an Animator state in the Animator window.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Random Start", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("State", state.name);

        var enabled = AnimatorStateBehaviourUtility.IsStartInRandomFrameEnabled(state);
        EditorGUI.BeginChangeCheck();
        var nextEnabled = EditorGUILayout.Toggle("Start in Random Frame", enabled);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(state, "Toggle Start in Random Frame");
        var behaviour = AnimatorStateBehaviourUtility.GetStartInRandomFrameBehaviour(state);
        if (behaviour != null)
            Undo.RecordObject(behaviour, "Toggle Start in Random Frame");

        AnimatorStateBehaviourUtility.SetStartInRandomFrameEnabled(state, nextEnabled);
    }
}

[InitializeOnLoad]
static class AnimatorStateStartInRandomFrameInspector
{
    static AnimatorStateStartInRandomFrameInspector()
    {
        Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
    }

    static void OnFinishedDefaultHeaderGUI(Editor editor)
    {
        if (editor == null || editor.target is not AnimatorState state)
            return;

        EditorGUILayout.Space(4f);
        AnimatorStateRandomStartToggleDrawer.Draw(state);
    }
}

public sealed class AnimatorRandomStartToolsWindow : EditorWindow
{
    [MenuItem("Window/Animation/Random Start Tools")]
    static void Open()
    {
        GetWindow<AnimatorRandomStartToolsWindow>("Random Start Tools");
    }

    void OnSelectionChange()
    {
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Fallback panel for toggling random frame start when the inline Animator state Inspector hook is unavailable.",
            MessageType.None);

        var state = Selection.activeObject as AnimatorState;
        AnimatorStateRandomStartToggleDrawer.Draw(state);
    }
}
