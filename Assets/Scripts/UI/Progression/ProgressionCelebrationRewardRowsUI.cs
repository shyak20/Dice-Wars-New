using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared row spawner for trial completed and rank-up celebration popups.</summary>
public static class ProgressionCelebrationRewardRowsUI
{
    public struct Prefabs
    {
        public CharacterInfoStat statRowPrefab;
        public GameObject relicRewardDisplayPrefab;
        public GameObject gemRewardDisplayPrefab;
        public GameObject addStartingDieRewardDisplayPrefab;
        public GameObject faceRewardDisplayPrefab;
        public TrialRewardRowElementUI compactRowPrefab;
        public TMP_Text textStyleTemplate;
    }

    public static void Populate(
        Transform parent,
        IReadOnlyList<ProgressionRewardDisplayEntry> entries,
        Prefabs prefabs,
        ProgressionRewardVisualCatalogSO catalog,
        List<GameObject> spawnedRows)
    {
        if (parent == null || entries == null || spawnedRows == null)
            return;

        var iconIndex = catalog != null ? catalog.IconIndex : null;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!entry.HasContent)
                continue;

            switch (entry.style)
            {
                case ProgressionRewardDisplayStyle.Stat:
                    SpawnStatRow(parent, entry, prefabs, iconIndex, spawnedRows);
                    break;
                case ProgressionRewardDisplayStyle.ItemDetail:
                    SpawnItemDetailRow(parent, entry, prefabs, spawnedRows);
                    break;
                case ProgressionRewardDisplayStyle.Compact:
                    SpawnCompactRow(parent, entry, prefabs, spawnedRows);
                    break;
            }
        }
    }

    static void SpawnStatRow(
        Transform parent,
        ProgressionRewardDisplayEntry entry,
        Prefabs prefabs,
        GameIconIndexSO iconIndex,
        List<GameObject> spawnedRows)
    {
        var template = prefabs.statRowPrefab;
        if (template == null)
            template = UnityEngine.Object.FindObjectOfType<CharacterInfoStat>(true);
        if (template == null || !entry.mainAttributeIconId.HasValue || !entry.statAmount.HasValue)
            return;

        var go = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        var stat = go.GetComponent<CharacterInfoStat>();
        if (stat == null)
        {
            UnityEngine.Object.Destroy(go);
            return;
        }

        stat.SetLabel(entry.statLabel ?? string.Empty);
        stat.SetMainAttributeIconId(entry.mainAttributeIconId.Value);
        stat.SetValue($"+{entry.statAmount.Value}", iconIndex);
        spawnedRows.Add(go);
    }

    static void SpawnItemDetailRow(
        Transform parent,
        ProgressionRewardDisplayEntry entry,
        Prefabs prefabs,
        List<GameObject> spawnedRows)
    {
        GameObject prefab;
        string sceneFallbackName;
        string rowName;

        if (entry.relic != null)
        {
            prefab = prefabs.relicRewardDisplayPrefab;
            sceneFallbackName = "Relic Reward";
            rowName = "Relic Unlock Row";
        }
        else if (entry.gem != null)
        {
            prefab = prefabs.gemRewardDisplayPrefab;
            sceneFallbackName = "Gem Reward";
            rowName = "Gem Unlock Row";
        }
        else if (entry.die != null)
        {
            prefab = prefabs.addStartingDieRewardDisplayPrefab;
            sceneFallbackName = null;
            rowName = "Add Starting Die Row";
        }
        else
        {
            SpawnTextRow(parent, entry.text, prefabs.textStyleTemplate, spawnedRows);
            return;
        }

        var go = InstantiateDisplayPrefab(parent, prefab, sceneFallbackName, rowName);
        if (go == null)
        {
            SpawnTextRow(parent, entry.text, prefabs.textStyleTemplate, spawnedRows);
            return;
        }

        var view = go.GetComponent<RankTrialRewardDisplay>();
        if (view != null)
        {
            if (entry.relic != null)
                view.BindRelic(entry.relic, entry.text);
            else if (entry.gem != null)
                view.BindGem(entry.gem, entry.text);
            else if (entry.die != null)
                view.BindDie(entry.die, entry.text);
        }
        else
        {
            TryBindLegacyRelicOrGemRow(go, entry.icon, entry.text, entry.gem != null);
        }

        spawnedRows.Add(go);
    }

    static void SpawnCompactRow(
        Transform parent,
        ProgressionRewardDisplayEntry entry,
        Prefabs prefabs,
        List<GameObject> spawnedRows)
    {
        if (entry.kind == ProgressionRewardVisualKind.UnlockFace && prefabs.faceRewardDisplayPrefab != null)
        {
            var go = InstantiateDisplayPrefab(parent, prefabs.faceRewardDisplayPrefab, "Relic Reward", "Face Unlock Row");
            if (go == null)
            {
                SpawnTextRow(parent, entry.text, prefabs.textStyleTemplate, spawnedRows);
                return;
            }

            var view = go.GetComponent<RankTrialRewardDisplay>();
            if (view != null)
            {
                var elementName = entry.faceUnlockDieType.HasValue
                    ? ProgressionRewardDisplayResolver.FormatDieTypeDisplayName(entry.faceUnlockDieType.Value)
                    : string.Empty;
                view.BindFaceUnlock(entry.icon, elementName, entry.text);
            }
            else
            {
                TryBindLegacyRelicOrGemRow(go, entry.icon, entry.text, false);
            }

            spawnedRows.Add(go);
            return;
        }

        if (prefabs.compactRowPrefab == null)
        {
            SpawnTextRow(parent, entry.text, prefabs.textStyleTemplate, spawnedRows);
            return;
        }

        var row = UnityEngine.Object.Instantiate(prefabs.compactRowPrefab, parent, false);
        row.Bind(entry);
        spawnedRows.Add(row.gameObject);
    }

    static GameObject InstantiateDisplayPrefab(
        Transform parent,
        GameObject prefab,
        string sceneFallbackName,
        string newName)
    {
        var template = prefab;
        if (template == null && !string.IsNullOrEmpty(sceneFallbackName))
            template = GameObject.Find(sceneFallbackName);

        if (template == null)
            return null;

        var go = UnityEngine.Object.Instantiate(template, parent, false);
        go.name = newName;
        go.SetActive(true);
        return go;
    }

    static void TryBindLegacyRelicOrGemRow(GameObject rowGo, Sprite iconSprite, string text, bool isGem)
    {
        if (rowGo == null)
            return;

        var tmpAll = rowGo.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text target = null;
        var desired = isGem ? "Die Gem" : "Artifact";
        if (tmpAll != null)
        {
            for (var i = 0; i < tmpAll.Length; i++)
            {
                var t = tmpAll[i];
                if (t == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(t.text) && t.text.IndexOf(desired, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    target = t;
                    break;
                }
            }
        }

        if (target == null && tmpAll != null && tmpAll.Length > 0)
            target = tmpAll[0];

        if (target != null)
            target.text = text ?? string.Empty;

        var images = rowGo.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
            return;

        Image icon = null;
        for (var i = 0; i < images.Length; i++)
        {
            var img = images[i];
            var n = img != null ? img.gameObject.name : string.Empty;
            if (!string.IsNullOrWhiteSpace(n) &&
                (n.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 n.IndexOf("Relic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 n.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                icon = img;
                break;
            }
        }

        if (icon == null && images.Length > 1)
            icon = images[1];
        if (icon == null)
            icon = images[0];

        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
    }

    static void SpawnTextRow(Transform parent, string text, TMP_Text styleTemplate, List<GameObject> spawnedRows)
    {
        if (styleTemplate == null)
            return;

        var go = new GameObject("Reward Text Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.font = styleTemplate.font;
        tmp.fontSize = styleTemplate.fontSize;
        tmp.color = styleTemplate.color;
        tmp.alignment = styleTemplate.alignment;
        tmp.enableWordWrapping = styleTemplate.enableWordWrapping;
        tmp.text = text ?? string.Empty;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);

        spawnedRows.Add(go);
    }
}
