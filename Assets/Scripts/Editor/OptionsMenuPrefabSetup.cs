#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds <see cref="OptionsMenuUI"/>, music volume slider, and abandon-run button to Settings Screen.prefab.</summary>
public static class OptionsMenuPrefabSetup
{
    const string MenuPath = "Dice Wars/UI/Setup Options Menu On Settings Prefab";
    const string PrefabPath = "Assets/Prefabs/UI/Settings Screen.prefab";

    [MenuItem(MenuPath)]
    public static void SetupSettingsPrefab()
    {
        var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"OptionsMenuPrefabSetup: could not load '{PrefabPath}'.");
            return;
        }

        var instance = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var settingsRoot = instance.transform;
            var optionsMenu = instance.GetComponent<OptionsMenuUI>();
            if (optionsMenu == null)
                optionsMenu = instance.AddComponent<OptionsMenuUI>();

            var volumeSlider = FindOrCreateMusicVolumeRow(settingsRoot);
            var abandonButton = FindOrCreateAbandonRunRow(settingsRoot);

            var so = new SerializedObject(optionsMenu);
            so.FindProperty("settingsRoot").objectReferenceValue = instance;
            so.FindProperty("musicVolumeSlider").objectReferenceValue = volumeSlider;
            so.FindProperty("abandonRunButton").objectReferenceValue = abandonButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Debug.Log("Options menu components added to Settings Screen.prefab.", optionsMenu);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    static UIMusicVolumeSlider FindOrCreateMusicVolumeRow(Transform settingsRoot)
    {
        var existing = settingsRoot.GetComponentInChildren<UIMusicVolumeSlider>(true);
        if (existing != null)
            return existing;

        var row = CreateRow(settingsRoot, "Music Volume", new Vector2(0f, 100f));
        var sliderGo = CreateUiObject("Slider", row.transform);
        var sliderRt = sliderGo.GetComponent<RectTransform>();
        SetAnchored(sliderRt, new Vector2(0f, 0f), new Vector2(220f, 28f));

        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = 1f;

        var bg = CreateUiObject("Background", sliderGo.transform);
        StretchFull(bg.GetComponent<RectTransform>());
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.18f, 0.25f, 1f);

        var fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        StretchFull(fillArea.GetComponent<RectTransform>());
        var fill = CreateUiObject("Fill", fillArea.transform);
        StretchFull(fill.GetComponent<RectTransform>());
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.25f, 0.55f, 0.95f, 1f);
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.targetGraphic = fillImage;

        var handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        StretchFull(handleArea.GetComponent<RectTransform>());
        var handle = CreateUiObject("Handle", handleArea.transform);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 20f);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        slider.handleRect = handleRt;

        var label = CreateTmp(row.transform, "Value Label", 18, TextAlignmentOptions.MidlineRight);
        SetAnchored(label.rectTransform, new Vector2(130f, 0f), new Vector2(60f, 28f));

        var volume = row.AddComponent<UIMusicVolumeSlider>();
        var volumeSo = new SerializedObject(volume);
        volumeSo.FindProperty("volumeSlider").objectReferenceValue = slider;
        volumeSo.FindProperty("valueLabel").objectReferenceValue = label;
        volumeSo.ApplyModifiedPropertiesWithoutUndo();

        return volume;
    }

    static UIAbandonRunButton FindOrCreateAbandonRunRow(Transform settingsRoot)
    {
        var existing = settingsRoot.GetComponentInChildren<UIAbandonRunButton>(true);
        if (existing != null)
            return existing;

        var row = CreateRow(settingsRoot, "Abandon Run", new Vector2(0f, 20f));
        var button = CreateButton(row.transform, "Abandon Run Button", "Abandon Run", Vector2.zero);
        var abandon = row.AddComponent<UIAbandonRunButton>();
        var abandonSo = new SerializedObject(abandon);
        abandonSo.FindProperty("abandonButton").objectReferenceValue = button;
        abandonSo.FindProperty("abandonControlRoot").objectReferenceValue = row;
        abandonSo.ApplyModifiedPropertiesWithoutUndo();
        return abandon;
    }

    static GameObject CreateRow(Transform parent, string name, Vector2 anchoredY)
    {
        var row = CreateUiObject(name, parent);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(320f, 40f);
        rt.anchoredPosition = anchoredY;

        var label = CreateTmp(row.transform, "Label", 20, TextAlignmentOptions.MidlineLeft);
        SetAnchored(label.rectTransform, new Vector2(-150f, 0f), new Vector2(140f, 32f));
        label.text = name;
        return row;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 pos)
    {
        var go = CreateUiObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        SetAnchored(rt, pos, new Vector2(220f, 44f));
        var image = go.AddComponent<Image>();
        image.color = new Color(0.55f, 0.18f, 0.18f, 1f);
        var button = go.AddComponent<Button>();
        var text = CreateTmp(go.transform, "Text", 20, TextAlignmentOptions.Center);
        StretchFull(text.rectTransform);
        text.text = label;
        text.raycastTarget = false;
        return button;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
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
