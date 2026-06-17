#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>Reorderable list UI for <see cref="FaceValueMatchSet.values"/> (works under SerializeReference actions).</summary>
[CustomPropertyDrawer(typeof(FaceValueMatchSet))]
public sealed class FaceValueMatchSetDrawer : PropertyDrawer
{
    const float Pad = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var values = property.FindPropertyRelative("values");
        if (values == null)
            return EditorGUIUtility.singleLineHeight;

        var list = CreateList(property.serializedObject, values, label.text);
        return list.GetHeight() + Pad;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var values = property.FindPropertyRelative("values");
        if (values == null)
        {
            EditorGUI.LabelField(position, label.text, "FaceValueMatchSet: missing 'values' list.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);
        var list = CreateList(property.serializedObject, values, label.text);
        var listRect = new Rect(position.x, position.y, position.width, list.GetHeight());
        list.DoList(listRect);
        EditorGUI.EndProperty();
    }

    static ReorderableList CreateList(SerializedObject serializedObject, SerializedProperty valuesProp, string header)
    {
        var list = new ReorderableList(serializedObject, valuesProp, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, header),
            drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = valuesProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, element, GUIContent.none);
            },
            elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4f
        };
        return list;
    }
}
#endif
