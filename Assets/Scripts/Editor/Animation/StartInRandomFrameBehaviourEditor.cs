using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StartInRandomFrameBehaviour))]
public sealed class StartInRandomFrameBehaviourEditor : Editor
{
    SerializedProperty _startInRandomFrame;

    void OnEnable()
    {
        _startInRandomFrame = serializedObject.FindProperty("startInRandomFrame");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_startInRandomFrame, new GUIContent("Start in Random Frame"));
        serializedObject.ApplyModifiedProperties();
    }
}
