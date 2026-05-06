using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(GameIconIndexSO.EnemyActionIconEntry))]
public sealed class EnemyActionIconEntryDrawer : PropertyDrawer
{
    private static readonly List<Type> ActionTypes = new List<Type>();
    private static readonly List<string> ActionTypeDisplayNames = new List<string>();
    private static readonly List<string> ActionTypeStoredNames = new List<string>();
    private static bool _cacheBuilt;

    private const float VerticalSpacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Dropdown + icon + background
        return (EditorGUIUtility.singleLineHeight * 3f) + (VerticalSpacing * 2f);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        BuildCacheIfNeeded();

        EditorGUI.BeginProperty(position, label, property);
        var actionTypeNameProp = property.FindPropertyRelative("actionTypeName");
        var iconProp = property.FindPropertyRelative("icon");
        var backgroundProp = property.FindPropertyRelative("background");

        var row1 = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var row2 = new Rect(position.x, row1.yMax + VerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
        var row3 = new Rect(position.x, row2.yMax + VerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);

        var currentStored = actionTypeNameProp.stringValue ?? string.Empty;
        var currentIndex = ResolveCurrentIndex(currentStored);
        var selectedIndex = EditorGUI.Popup(row1, "Enemy Action", currentIndex, ActionTypeDisplayNames.ToArray());
        if (selectedIndex != currentIndex && selectedIndex >= 0 && selectedIndex < ActionTypeStoredNames.Count)
            actionTypeNameProp.stringValue = ActionTypeStoredNames[selectedIndex];

        EditorGUI.PropertyField(row2, iconProp);
        EditorGUI.PropertyField(row3, backgroundProp);
        EditorGUI.EndProperty();
    }

    private static int ResolveCurrentIndex(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
            return 0;

        for (var i = 0; i < ActionTypeStoredNames.Count; i++)
        {
            var candidate = ActionTypeStoredNames[i];
            if (string.Equals(storedName, candidate, StringComparison.Ordinal))
                return i;
        }

        // Allow old short names to map to full names.
        for (var i = 0; i < ActionTypeStoredNames.Count; i++)
        {
            var candidate = ActionTypeStoredNames[i];
            var dot = candidate.LastIndexOf('.');
            var shortName = dot >= 0 && dot + 1 < candidate.Length ? candidate.Substring(dot + 1) : candidate;
            if (string.Equals(storedName, shortName, StringComparison.Ordinal))
                return i;
        }

        return 0;
    }

    private static void BuildCacheIfNeeded()
    {
        if (_cacheBuilt)
            return;

        ActionTypes.Clear();
        ActionTypeDisplayNames.Clear();
        ActionTypeStoredNames.Clear();

        ActionTypeDisplayNames.Add("<None>");
        ActionTypeStoredNames.Add(string.Empty);

        var types = TypeCache.GetTypesDerivedFrom<IGameAction>()
            .Where(t => t != null && !t.IsAbstract && !t.IsInterface && !t.IsGenericType)
            .OrderBy(t => t.Name)
            .ThenBy(t => t.FullName)
            .ToList();

        for (var i = 0; i < types.Count; i++)
        {
            var t = types[i];
            ActionTypes.Add(t);
            ActionTypeDisplayNames.Add(t.Name);
            ActionTypeStoredNames.Add(t.FullName ?? t.Name);
        }

        _cacheBuilt = true;
    }
}
