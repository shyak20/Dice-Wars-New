using UnityEditor;
using UnityEditor.Animations;

static class AnimatorStateBehaviourUtility
{
    public static StartInRandomFrameBehaviour GetStartInRandomFrameBehaviour(AnimatorState state)
    {
        if (state == null)
            return null;

        var behaviours = state.behaviours;
        if (behaviours == null)
            return null;

        for (var i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is StartInRandomFrameBehaviour randomStart)
                return randomStart;
        }

        return null;
    }

    public static StartInRandomFrameBehaviour GetOrAddStartInRandomFrameBehaviour(AnimatorState state)
    {
        var existing = GetStartInRandomFrameBehaviour(state);
        if (existing != null)
            return existing;

        return state.AddStateMachineBehaviour<StartInRandomFrameBehaviour>();
    }

    public static bool IsStartInRandomFrameEnabled(AnimatorState state)
    {
        var behaviour = GetStartInRandomFrameBehaviour(state);
        return behaviour != null && behaviour.StartInRandomFrame;
    }

    public static void SetStartInRandomFrameEnabled(AnimatorState state, bool enabled)
    {
        if (state == null)
            return;

        var behaviour = GetStartInRandomFrameBehaviour(state);
        if (enabled)
        {
            behaviour = GetOrAddStartInRandomFrameBehaviour(state);
            behaviour.StartInRandomFrame = true;
            MarkControllerDirty(behaviour);
            return;
        }

        if (behaviour == null)
            return;

        behaviour.StartInRandomFrame = false;
        MarkControllerDirty(behaviour);
    }

    static void MarkControllerDirty(StartInRandomFrameBehaviour behaviour)
    {
        if (behaviour == null)
            return;

        EditorUtility.SetDirty(behaviour);
        var path = AssetDatabase.GetAssetPath(behaviour);
        if (string.IsNullOrEmpty(path))
            return;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller != null)
            EditorUtility.SetDirty(controller);
    }
}
