#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates trial reward tooltip prefabs and wires <see cref="HoverTooltipManager.trialRewardsPanelPrefab"/> on selection.</summary>
public static class ProgressionTrialRewardsTooltipPrefabSetup
{
    const string TooltipsFolder = "Assets/Prefabs/UI/Tooltips";
    const string RowPrefabPath = TooltipsFolder + "/Trial Reward Row Element.prefab";
    const string PanelPrefabPath = TooltipsFolder + "/Trial Rewards Tooltip.prefab";

    [MenuItem("Dice Wars/UI/Setup Trial Rewards Tooltip Prefabs")]
    public static void SetupPrefabs()
    {
        EnsureFolder(TooltipsFolder);

        var rowPrefab = CreateOrLoadRowPrefab();
        var panelPrefab = CreateOrLoadPanelPrefab(rowPrefab);
        WireHoverTooltipManagers(panelPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Trial rewards tooltip prefabs ready:\n  {RowPrefabPath}\n  {PanelPrefabPath}");
    }

    static TrialRewardRowElementUI CreateOrLoadRowPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
        if (existing != null)
            return existing.GetComponent<TrialRewardRowElementUI>();

        var root = new GameObject("Trial Reward Row Element", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 32f);

        var layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(28f, 28f);
        var icon = iconGo.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(root.transform, false);
        var label = textGo.GetComponent<TextMeshProUGUI>();
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "+1 Max HP";
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(240f, 28f);

        var row = root.AddComponent<TrialRewardRowElementUI>();
        var so = new SerializedObject(row);
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("labelText").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<TrialRewardRowElementUI>();
    }

    static HoverTrialRewardsTooltipPanelUI CreateOrLoadPanelPrefab(TrialRewardRowElementUI rowPrefab)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
        if (existing != null)
        {
            var existingPanel = existing.GetComponent<HoverTrialRewardsTooltipPanelUI>();
            if (existingPanel != null)
                WirePanelReferences(existingPanel, rowPrefab);
            return existingPanel;
        }

        var root = new GameObject("Trial Rewards Tooltip", typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(340f, 200f);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.09f, 0.95f);
        bg.raycastTarget = false;

        var vLayout = root.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(12, 12, 12, 12);
        vLayout.spacing = 8f;
        vLayout.childAlignment = TextAnchor.UpperLeft;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        var titleGo = CreateTextChild(root.transform, "Title", 22f, FontStyles.Bold, "Trial Title");
        var descGo = CreateTextChild(root.transform, "Description", 16f, FontStyles.Normal, "Trial description.");
        var rewardsGo = new GameObject("Rewards Layout", typeof(RectTransform));
        rewardsGo.transform.SetParent(root.transform, false);
        var rewardsRt = rewardsGo.GetComponent<RectTransform>();
        rewardsRt.sizeDelta = new Vector2(0f, 80f);
        var rewardsLayout = rewardsGo.AddComponent<VerticalLayoutGroup>();
        rewardsLayout.spacing = 4f;
        rewardsLayout.childAlignment = TextAnchor.UpperLeft;
        rewardsLayout.childControlWidth = true;
        rewardsLayout.childControlHeight = true;
        rewardsLayout.childForceExpandWidth = true;
        rewardsLayout.childForceExpandHeight = false;

        var tooltipPanel = root.AddComponent<HoverTrialRewardsTooltipPanelUI>();
        WirePanelReferences(tooltipPanel, rowPrefab, titleGo, descGo, rewardsGo.transform);

        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        Object.DestroyImmediate(root);
        return savedPrefab.GetComponent<HoverTrialRewardsTooltipPanelUI>();
    }

    static void WirePanelReferences(
        HoverTrialRewardsTooltipPanelUI panel,
        TrialRewardRowElementUI rowPrefab,
        TMP_Text title = null,
        TMP_Text description = null,
        Transform rewardLayout = null)
    {
        if (panel == null)
            return;

        var so = new SerializedObject(panel);
        so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;

        if (title == null)
        {
            var titleTr = panel.transform.Find("Title");
            if (titleTr != null)
                title = titleTr.GetComponent<TMP_Text>();
        }

        if (description == null)
        {
            var descTr = panel.transform.Find("Description");
            if (descTr != null)
                description = descTr.GetComponent<TMP_Text>();
        }

        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("descriptionText").objectReferenceValue = description;

        if (rewardLayout == null)
        {
            var rewards = panel.transform.Find("Rewards Layout");
            if (rewards != null)
                rewardLayout = rewards;
        }

        so.FindProperty("rewardLayoutRoot").objectReferenceValue = rewardLayout;
        so.FindProperty("rewardRowPrefab").objectReferenceValue = rowPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static TMP_Text CreateTextChild(Transform parent, string name, float fontSize, FontStyles style, string sample)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = sample;
        return text;
    }

    static void WireHoverTooltipManagers(HoverTrialRewardsTooltipPanelUI panelPrefab)
    {
        var managers = Object.FindObjectsOfType<HoverTooltipManager>(true);
        for (var i = 0; i < managers.Length; i++)
        {
            var mgr = managers[i];
            var so = new SerializedObject(mgr);
            so.FindProperty("trialRewardsPanelPrefab").objectReferenceValue = panelPrefab;
            var icon = AssetDatabase.LoadAssetAtPath<GameIconIndexSO>("Assets/Data/GameIconIndex.asset");
            if (icon != null)
                so.FindProperty("gameIconIndex").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mgr);
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        var leaf = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent ?? "Assets", leaf);
    }
}
#endif
