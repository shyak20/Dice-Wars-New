#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds trial title + progress slider to <c>Trial Display UI Prefab</c> and wires <see cref="ProgressionTrialSlotUI"/>.</summary>
public static class ProgressionTrialSlotPrefabSetup
{
    const string MenuPath = "Dice Wars/UI/Setup Trial Display Slot Prefab";
    const string PrefabPath = "Assets/Prefabs/UI/Trial Display UI Prefab.prefab";

    [MenuItem(MenuPath)]
    public static void SetupTrialDisplayPrefab()
    {
        var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"ProgressionTrialSlotPrefabSetup: could not load '{PrefabPath}'.");
            return;
        }

        var instance = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var slot = instance.GetComponent<ProgressionTrialSlotUI>();
            if (slot == null)
            {
                Debug.LogError("ProgressionTrialSlotPrefabSetup: ProgressionTrialSlotUI missing on prefab root.", instance);
                return;
            }

            var rootRt = instance.GetComponent<RectTransform>();
            var title = FindOrCreateTitle(rootRt);
            var slider = FindOrCreateProgressSlider(rootRt);

            var so = new SerializedObject(slot);
            so.FindProperty("trialTitleText").objectReferenceValue = title;
            so.FindProperty("progressSlider").objectReferenceValue = slider;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Debug.Log("Trial title + progress slider wired on Trial Display UI Prefab.", slot);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    static TMP_Text FindOrCreateTitle(RectTransform root)
    {
        var existing = root.Find("Trial Title");
        if (existing != null)
        {
            var tmp = existing.GetComponent<TMP_Text>();
            if (tmp != null)
                return tmp;
        }

        var go = CreateUiObject("Trial Title", root);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(130.7f, -8f);
        rt.sizeDelta = new Vector2(-15.8f, 28f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = 22f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "Trial Name";
        return text;
    }

    static Slider FindOrCreateProgressSlider(RectTransform root)
    {
        var existing = root.Find("Trial Progress Slider");
        if (existing != null)
        {
            var existingSlider = existing.GetComponent<Slider>();
            if (existingSlider != null)
                return existingSlider;
        }

        var sliderGo = CreateUiObject("Trial Progress Slider", root);
        var sliderRt = sliderGo.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0f, 0f);
        sliderRt.anchorMax = new Vector2(1f, 0f);
        sliderRt.pivot = new Vector2(0.5f, 0f);
        sliderRt.anchoredPosition = new Vector2(130.7f, 52f);
        sliderRt.sizeDelta = new Vector2(-40f, 14f);

        var progressSlider = sliderGo.AddComponent<Slider>();
        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.wholeNumbers = true;
        progressSlider.value = 0f;
        progressSlider.interactable = false;

        var bg = CreateUiObject("Background", sliderGo.transform);
        StretchFull(bg.GetComponent<RectTransform>());
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.14f, 0.2f, 0.9f);
        bgImage.raycastTarget = false;

        var fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        StretchFull(fillArea.GetComponent<RectTransform>());
        var fill = CreateUiObject("Fill", fillArea.transform);
        StretchFull(fill.GetComponent<RectTransform>());
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.35f, 0.72f, 1f, 1f);
        fillImage.raycastTarget = false;
        progressSlider.fillRect = fill.GetComponent<RectTransform>();
        progressSlider.targetGraphic = fillImage;

        return progressSlider;
    }

    static GameObject CreateUiObject(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
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
}
#endif
