#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates Dice Select progression celebration popups and wires <see cref="DiceSelectProgressionCelebrationController"/>.</summary>
public static class ProgressionCelebrationUISetup
{
    const string MenuPath = "Dice Wars/UI/Setup Progression Celebration Popups (Dice Select)";

    [MenuItem(MenuPath)]
    public static void SetupInDiceSelectScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("DiceSelect.unity", System.StringComparison.OrdinalIgnoreCase))
        {
            if (!EditorUtility.DisplayDialog(
                    "Progression celebration UI",
                    "Open Assets/Scenes/DiceSelect.unity first, then run this menu again.",
                    "Open Dice Select",
                    "Cancel"))
                return;

            EditorSceneManager.OpenScene("Assets/Scenes/DiceSelect.unity");
            scene = SceneManager.GetActiveScene();
        }

        var controller = Object.FindObjectOfType<DiceSelectSceneController>(true);
        if (controller == null)
        {
            Debug.LogError("ProgressionCelebrationUISetup: DiceSelectSceneController not found in scene.");
            return;
        }

        var canvasRt = Object.FindObjectOfType<Canvas>(true)?.transform as RectTransform;
        if (canvasRt == null)
        {
            Debug.LogError("ProgressionCelebrationUISetup: no Canvas in scene.");
            return;
        }

        var root = FindOrCreateRoot(canvasRt);
        var trialPopup = FindOrCreateTrialPopup(root);
        var rankPopup = FindOrCreateRankUpPopup(root);
        var blocker = FindOrCreateBlocker(root);

        var celebration = controller.GetComponent<DiceSelectProgressionCelebrationController>();
        if (celebration == null)
            celebration = controller.gameObject.AddComponent<DiceSelectProgressionCelebrationController>();

        var so = new SerializedObject(celebration);
        so.FindProperty("diceSelectSceneController").objectReferenceValue = controller;
        so.FindProperty("trialCompletedPopup").objectReferenceValue = trialPopup;
        so.FindProperty("rankUpPopup").objectReferenceValue = rankPopup;
        so.FindProperty("progressionCelebrationRoot").objectReferenceValue = root.gameObject;
        so.FindProperty("inputBlocker").objectReferenceValue = blocker;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Progression celebration UI created on DiceSelect. Save the scene.", celebration);
    }

    static Transform FindOrCreateRoot(RectTransform canvasRt)
    {
        var existing = canvasRt.Find("Progression Celebrations");
        if (existing != null)
            return existing;

        var go = new GameObject("Progression Celebrations", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvasRt, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
        return rt;
    }

    static GameObject FindOrCreateBlocker(Transform root)
    {
        var existing = root.Find("Input Blocker");
        if (existing != null)
            return existing.gameObject;

        var go = CreateUiObject("Input Blocker", root);
        var rt = go.GetComponent<RectTransform>();
        StretchFull(rt);
        var image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);
        image.raycastTarget = true;
        go.SetActive(false);
        return go;
    }

    static ProgressionTrialCompletedPopupView FindOrCreateTrialPopup(Transform root)
    {
        var existing = root.Find("Trial Completed Popup");
        if (existing != null && existing.TryGetComponent(out ProgressionTrialCompletedPopupView view))
            return view;

        return BuildTrialPopup(root);
    }

    static ProgressionRankUpPopupView FindOrCreateRankUpPopup(Transform root)
    {
        var existing = root.Find("Rank Up Popup");
        if (existing != null && existing.TryGetComponent(out ProgressionRankUpPopupView view))
            return view;

        return BuildRankUpPopup(root);
    }

    static ProgressionTrialCompletedPopupView BuildTrialPopup(Transform root)
    {
        var popupRoot = CreateUiObject("Trial Completed Popup", root);
        var popupRt = popupRoot.GetComponent<RectTransform>();
        StretchFull(popupRt);

        var panel = CreatePanel(popupRoot.transform, "Panel", new Vector2(520f, 360f));
        var title = CreateTmp(panel.transform, "Title", 28, TextAlignmentOptions.Center);
        SetAnchored(title.rectTransform, new Vector2(0f, 120f), new Vector2(480f, 48f));
        title.text = "Trial Complete";

        var iconGo = CreateUiObject("Icon", panel.transform);
        var iconRt = iconGo.GetComponent<RectTransform>();
        SetAnchored(iconRt, new Vector2(0f, 40f), new Vector2(96f, 96f));
        var icon = iconGo.AddComponent<Image>();

        var body = CreateTmp(panel.transform, "Body", 20, TextAlignmentOptions.Top);
        SetAnchored(body.rectTransform, new Vector2(0f, -40f), new Vector2(460f, 120f));
        body.text = "Description";

        var completeBtn = CreateButton(panel.transform, "Complete Button", "Complete", new Vector2(0f, -130f));

        var view = popupRoot.AddComponent<ProgressionTrialCompletedPopupView>();
        var so = new SerializedObject(view);
        so.FindProperty("panelRoot").objectReferenceValue = popupRoot;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("trialIconImage").objectReferenceValue = icon;
        so.FindProperty("completeButton").objectReferenceValue = completeBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        popupRoot.SetActive(false);
        return view;
    }

    static ProgressionRankUpPopupView BuildRankUpPopup(Transform root)
    {
        var popupRoot = CreateUiObject("Rank Up Popup", root);
        var popupRt = popupRoot.GetComponent<RectTransform>();
        StretchFull(popupRt);

        var panel = CreatePanel(popupRoot.transform, "Panel", new Vector2(560f, 400f));
        var title = CreateTmp(panel.transform, "Title", 32, TextAlignmentOptions.Center);
        SetAnchored(title.rectTransform, new Vector2(0f, 140f), new Vector2(520f, 56f));
        title.text = "Level Up";

        var body = CreateTmp(panel.transform, "Body", 20, TextAlignmentOptions.Top);
        SetAnchored(body.rectTransform, new Vector2(0f, -20f), new Vector2(500f, 180f));
        body.text = "All trials complete.";

        var completeBtn = CreateButton(panel.transform, "Complete Button", "Complete", new Vector2(0f, -150f));

        var view = popupRoot.AddComponent<ProgressionRankUpPopupView>();
        var so = new SerializedObject(view);
        so.FindProperty("panelRoot").objectReferenceValue = popupRoot;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("completeButton").objectReferenceValue = completeBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        popupRoot.SetActive(false);
        return view;
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        var panel = CreateUiObject(name, parent);
        var rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        var image = panel.AddComponent<Image>();
        image.color = new Color(0.08f, 0.1f, 0.16f, 0.96f);
        return panel;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        var go = CreateUiObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        SetAnchored(rt, anchoredPosition, new Vector2(200f, 48f));
        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.45f, 0.85f, 1f);
        var button = go.AddComponent<Button>();

        var text = CreateTmp(go.transform, "Text", 22, TextAlignmentOptions.Center);
        StretchFull(text.rectTransform);
        text.text = label;
        text.raycastTarget = false;

        return button;
    }

    static TMP_Text CreateTmp(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
    {
        var go = CreateUiObject(name, parent);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAnchored(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}
#endif
