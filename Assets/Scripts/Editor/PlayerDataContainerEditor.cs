using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerDataContainer))]
public class PlayerDataContainerEditor : Editor
{
    private bool[] dieFoldouts = new bool[0];

    static Sprite TryGetElementIconForEditor(DieType type)
    {
        var guids = AssetDatabase.FindAssets("t:GameIconIndexSO");
        if (guids.Length == 0) return null;
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var index = AssetDatabase.LoadAssetAtPath<GameIconIndexSO>(path);
        return index != null ? index.GetElementIcon(type) : null;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var container = (PlayerDataContainer)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Runtime Deck (Play Mode Only)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to inspect the runtime deck.", MessageType.Info);
            return;
        }

        if (container.RuntimeData == null)
        {
            EditorGUILayout.HelpBox("RuntimeData is null.", MessageType.Warning);
            return;
        }

        var deck = container.RuntimeData.currentDeck;
        if (deck == null || deck.Count == 0)
        {
            EditorGUILayout.HelpBox("Deck is empty.", MessageType.Warning);
            return;
        }

        if (dieFoldouts.Length != deck.Count)
            dieFoldouts = new bool[deck.Count];

        for (var d = 0; d < deck.Count; d++)
        {
            var die = deck[d];
            if (die == null)
            {
                EditorGUILayout.LabelField($"Die {d}: (null)");
                continue;
            }

            dieFoldouts[d] = EditorGUILayout.Foldout(dieFoldouts[d], $"{die.dieName} ({die.dieType})", true, EditorStyles.foldoutHeader);

            if (!dieFoldouts[d]) continue;

            EditorGUI.indentLevel++;

            if (die.faces == null || die.faces.Length == 0)
            {
                EditorGUILayout.LabelField("No faces.");
                EditorGUI.indentLevel--;
                continue;
            }

            for (var f = 0; f < die.faces.Length; f++)
            {
                var face = die.faces[f];
                if (face == null)
                {
                    EditorGUILayout.LabelField($"  Slot {f}: (null)");
                    continue;
                }

                EditorGUILayout.BeginHorizontal();

                var deckIcon = TryGetElementIconForEditor(face.type);
                if (deckIcon != null)
                {
                    var iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));
                    GUI.DrawTexture(iconRect, deckIcon.texture, ScaleMode.ScaleToFit);
                }

                EditorGUILayout.LabelField($"[{f}] {face.Title}", GUILayout.Width(140));
                EditorGUILayout.LabelField($"Val:{face.value}", GUILayout.Width(50));
                EditorGUILayout.LabelField($"{face.type}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{face.rarity}", GUILayout.Width(70));
                EditorGUILayout.ObjectField(face, typeof(DieFaceSO), false, GUILayout.Width(120));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        if (GUILayout.Button("Refresh"))
        {
            Repaint();
        }
    }
}
