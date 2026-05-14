using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using System.Linq;
using System;

public class GameIconIndexTmpAtlasTool : EditorWindow
{
    private const int PrivateUseAreaStart = 0xE000;

    /// <summary>EditorWindow object references are not reliably restored from layout; we persist via EditorPrefs per project.</summary>
    private static string PrefsKeyRoot
    {
        get
        {
            var projectId = PlayerSettings.productGUID;
            var id = projectId == Guid.Empty ? Application.dataPath : projectId.ToString("N");
            return $"DiceWars.GameIconIndexTmpAtlasTool.{id}";
        }
    }

    [SerializeField] private GameIconIndexSO iconIndex;
    [Tooltip("Optional. All Sprites under each folder (recursive) are packed; atlas sprite names match each Sprite asset name in Unity.")]
    [SerializeField] private List<DefaultAsset> additionalIconFolders = new List<DefaultAsset>();
    [SerializeField] private DefaultAsset outputFolder;
    [SerializeField] private string atlasBaseName = "GameIconIndexTMPAtlas";
    [SerializeField] private int atlasSize = 1024;
    [SerializeField] private int atlasPadding = 2;
    [SerializeField] private bool setAsTmpDefaultSpriteAsset = true;
    [Tooltip("Added to every baked sprite glyph’s Horizontal Bearing X (BX) after generation. Example: existing 0 + tool 0 → 0.")]
    [SerializeField] private int addToBakedGlyphBearingX;
    [Tooltip("Added to every baked sprite glyph’s Horizontal Bearing Y (BY) after generation. Example: existing 55 + tool 23 → 78.")]
    [SerializeField] private int addToBakedGlyphBearingY;

    [MenuItem("Tools/Dice Wars/Create TMP Atlas From GameIconIndex")]
    public static void Open()
    {
        var window = GetWindow<GameIconIndexTmpAtlasTool>("TMP Atlas From IconIndex");
        window.minSize = new Vector2(480f, 260f);
        window.Show();
    }

    private void OnEnable() => LoadPersistedAssignments();

    private void OnDisable() => SavePersistedAssignments();

    private void LoadPersistedAssignments()
    {
        var root = PrefsKeyRoot;
        iconIndex = LoadObjectFromGuidPref<GameIconIndexSO>(root, "IconIndex");
        outputFolder = LoadObjectFromGuidPref<DefaultAsset>(root, "OutputFolder");

        if (additionalIconFolders == null)
            additionalIconFolders = new List<DefaultAsset>();
        additionalIconFolders.Clear();
        var foldersRaw = EditorPrefs.GetString($"{root}.ExtraFolders", "");
        if (!string.IsNullOrEmpty(foldersRaw))
        {
            foreach (var token in foldersRaw.Split('|'))
            {
                if (string.IsNullOrEmpty(token))
                    additionalIconFolders.Add(null);
                else
                {
                    var path = AssetDatabase.GUIDToAssetPath(token);
                    additionalIconFolders.Add(
                        string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<DefaultAsset>(path));
                }
            }
        }

        atlasBaseName = EditorPrefs.GetString($"{root}.AtlasBaseName", atlasBaseName);
        atlasSize = EditorPrefs.GetInt($"{root}.AtlasSize", atlasSize);
        atlasPadding = EditorPrefs.GetInt($"{root}.AtlasPadding", atlasPadding);
        setAsTmpDefaultSpriteAsset = EditorPrefs.GetBool($"{root}.SetAsTmpDefault", setAsTmpDefaultSpriteAsset);
        addToBakedGlyphBearingX = EditorPrefs.GetInt($"{root}.AddBx", addToBakedGlyphBearingX);
        addToBakedGlyphBearingY = EditorPrefs.GetInt($"{root}.AddBy", addToBakedGlyphBearingY);
    }

    private void SavePersistedAssignments()
    {
        var root = PrefsKeyRoot;
        SetGuidPref(root, "IconIndex", iconIndex);
        SetGuidPref(root, "OutputFolder", outputFolder);

        if (additionalIconFolders == null)
            additionalIconFolders = new List<DefaultAsset>();
        var folderTokens = new List<string>(additionalIconFolders.Count);
        foreach (var folder in additionalIconFolders)
        {
            if (folder == null)
                folderTokens.Add("");
            else
            {
                var path = AssetDatabase.GetAssetPath(folder);
                var guid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
                folderTokens.Add(guid ?? "");
            }
        }

        EditorPrefs.SetString($"{root}.ExtraFolders", string.Join("|", folderTokens));
        EditorPrefs.SetString($"{root}.AtlasBaseName", atlasBaseName ?? "GameIconIndexTMPAtlas");
        EditorPrefs.SetInt($"{root}.AtlasSize", atlasSize);
        EditorPrefs.SetInt($"{root}.AtlasPadding", atlasPadding);
        EditorPrefs.SetBool($"{root}.SetAsTmpDefault", setAsTmpDefaultSpriteAsset);
        EditorPrefs.SetInt($"{root}.AddBx", addToBakedGlyphBearingX);
        EditorPrefs.SetInt($"{root}.AddBy", addToBakedGlyphBearingY);
    }

    private static void SetGuidPref(string prefsRoot, string suffix, UnityEngine.Object obj)
    {
        if (obj == null)
        {
            EditorPrefs.SetString($"{prefsRoot}.{suffix}", "");
            return;
        }

        var path = AssetDatabase.GetAssetPath(obj);
        var guid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        EditorPrefs.SetString($"{prefsRoot}.{suffix}", guid ?? "");
    }

    private static T LoadObjectFromGuidPref<T>(string prefsRoot, string suffix) where T : UnityEngine.Object
    {
        var guid = EditorPrefs.GetString($"{prefsRoot}.{suffix}", "");
        if (string.IsNullOrEmpty(guid))
            return null;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
            return null;
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Generate TMP Sprite Atlas", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Reads icons from GameIconIndexSO and/or the folders below, packs them into one atlas texture, then creates a TMP Sprite Asset. " +
            "Sprite names in the atlas match each source Sprite’s name in Unity (first wins if two sprites share a name). " +
            "Object fields and options are saved for this project when the window closes (survives reopening Unity).",
            MessageType.Info);

        iconIndex = (GameIconIndexSO)EditorGUILayout.ObjectField("Game Icon Index", iconIndex, typeof(GameIconIndexSO), false);
        EditorGUILayout.LabelField("Additional Icon Folders (recursive)", EditorStyles.boldLabel);
        for (var i = 0; i < additionalIconFolders.Count; i++)
            additionalIconFolders[i] = (DefaultAsset)EditorGUILayout.ObjectField($"Folder {i + 1}", additionalIconFolders[i], typeof(DefaultAsset), false);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Folder", GUILayout.Width(100f)))
                additionalIconFolders.Add(null);
            if (additionalIconFolders.Count > 0 && GUILayout.Button("Remove Last", GUILayout.Width(100f)))
                additionalIconFolders.RemoveAt(additionalIconFolders.Count - 1);
        }
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
        atlasBaseName = EditorGUILayout.TextField("Asset Base Name", atlasBaseName);
        atlasSize = Mathf.Clamp(EditorGUILayout.IntField("Atlas Size", atlasSize), 128, 8192);
        atlasPadding = Mathf.Clamp(EditorGUILayout.IntField("Atlas Padding", atlasPadding), 0, 64);
        setAsTmpDefaultSpriteAsset = EditorGUILayout.ToggleLeft("Set generated atlas as TMP default sprite asset", setAsTmpDefaultSpriteAsset);
        addToBakedGlyphBearingX = EditorGUILayout.IntField(new GUIContent("Add to baked BX", "Added to each glyph’s Horizontal Bearing X after bake."), addToBakedGlyphBearingX);
        addToBakedGlyphBearingY = EditorGUILayout.IntField(new GUIContent("Add to baked BY", "Added to each glyph’s Horizontal Bearing Y after bake."), addToBakedGlyphBearingY);

        EditorGUILayout.Space(12f);
        var hasFolder = additionalIconFolders != null && additionalIconFolders.Exists(f => f != null);
        using (new EditorGUI.DisabledScope(iconIndex == null && !hasFolder))
        {
            if (GUILayout.Button("Create TMP Atlas Assets", GUILayout.Height(32f)))
                Generate();
        }
    }

    private void Generate()
    {
        var hasFolder = additionalIconFolders != null && additionalIconFolders.Exists(f => f != null);
        if (iconIndex == null && !hasFolder)
        {
            Debug.LogError("TMP Atlas Tool: assign a GameIconIndexSO and/or at least one additional icon folder.");
            return;
        }

        var folderPath = ResolveOutputFolder();
        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogError("TMP Atlas Tool: output folder must be inside Assets/.");
            return;
        }

        var uniqueSprites = new List<Sprite>();
        var seenSprites = new HashSet<Sprite>();
        var usedNames = new HashSet<string>();

        if (iconIndex != null)
        {
            foreach (var entry in iconIndex.GetAllIconEntries())
                AddSpriteIfUnique(entry.sprite, uniqueSprites, seenSprites, usedNames);
        }

        if (additionalIconFolders != null)
        {
            foreach (var folderAsset in additionalIconFolders)
            {
                if (folderAsset == null) continue;
                var path = AssetDatabase.GetAssetPath(folderAsset);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    Debug.LogWarning($"TMP Atlas Tool: '{path}' is not a project folder; skipping.");
                    continue;
                }

                CollectSpritesFromFolderRecursive(path, uniqueSprites, seenSprites, usedNames);
            }
        }

        if (uniqueSprites.Count == 0)
        {
            Debug.LogError("TMP Atlas Tool: no sprites found (check GameIconIndexSO and icon folders).");
            return;
        }

        var iconTextures = new Texture2D[uniqueSprites.Count];
        for (var i = 0; i < uniqueSprites.Count; i++)
            iconTextures[i] = ExtractSpriteTexture(uniqueSprites[i]);

        var atlasTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
        atlasTexture.name = atlasBaseName + "_Texture";
        var packedRects = atlasTexture.PackTextures(iconTextures, atlasPadding, atlasSize, false);
        atlasTexture.Apply(false, false);

        var atlasPngPath = Path.Combine(folderPath, atlasBaseName + "_Texture.png");
        File.WriteAllBytes(atlasPngPath, atlasTexture.EncodeToPNG());
        AssetDatabase.ImportAsset(atlasPngPath, ImportAssetOptions.ForceSynchronousImport);

        ConfigureAtlasImporter(atlasPngPath, atlasTexture.width, atlasTexture.height, packedRects, uniqueSprites);

        var importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPngPath);
        if (importedAtlas == null)
        {
            Debug.LogError("TMP Atlas Tool: failed to import atlas texture.");
            return;
        }

        var spriteAssetPath = Path.Combine(folderPath, atlasBaseName + ".asset");
        var materialPath = Path.Combine(folderPath, atlasBaseName + "_Material.mat");

        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("TextMeshPro/Sprite"))
            {
                name = atlasBaseName + "_Material"
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.mainTexture = importedAtlas;
        EditorUtility.SetDirty(material);

        var spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(spriteAssetPath);
        if (spriteAsset == null)
        {
            spriteAsset = CreateInitializedSpriteAsset(importedAtlas);
            spriteAsset.name = atlasBaseName;
            AssetDatabase.CreateAsset(spriteAsset, spriteAssetPath);
        }

        spriteAsset.spriteSheet = importedAtlas;
        spriteAsset.material = material;
        spriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(spriteAsset.name);
        PopulateLegacySpriteInfoFromAtlas(spriteAsset, atlasPngPath);

        if (!TryUpdateLookupTablesSafe(spriteAsset))
        {
            Debug.LogError("TMP Atlas Tool: Failed to update TMP sprite lookup tables after compatibility initialization.");
            return;
        }

        FixCharacterGlyphLinks(spriteAsset);
        if (!TryUpdateLookupTablesSafe(spriteAsset))
        {
            Debug.LogError("TMP Atlas Tool: Failed to rebuild TMP sprite lookup tables after glyph-link fix.");
            return;
        }

        AddToAllSpriteGlyphBearings(spriteAsset, addToBakedGlyphBearingX, addToBakedGlyphBearingY);

        EditorUtility.SetDirty(spriteAsset);

        if (setAsTmpDefaultSpriteAsset)
            SetTmpDefaultSpriteAsset(spriteAsset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(spriteAsset);
        Debug.Log($"TMP Atlas Tool: Created atlas '{atlasBaseName}' with {uniqueSprites.Count} sprites at '{folderPath}'.");
        SavePersistedAssignments();
    }

    private static void SetTmpDefaultSpriteAsset(TMP_SpriteAsset spriteAsset)
    {
        if (spriteAsset == null) return;

        // Preferred path: TMP_Settings.defaultSpriteAsset static property.
        try
        {
            var settingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro");
            if (settingsType != null)
            {
                var prop = settingsType.GetProperty("defaultSpriteAsset", BindingFlags.Public | BindingFlags.Static);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(null, spriteAsset, null);
                    Debug.Log($"TMP Atlas Tool: Set TMP default sprite asset to '{spriteAsset.name}'.");
                    return;
                }
            }
        }
        catch
        {
            // Fall through to serialized fallback.
        }

        // Fallback: edit TMP Settings asset directly by serialized property name.
        try
        {
            var settingsObj = Resources.Load("TMP Settings");
            if (settingsObj == null)
            {
                Debug.LogWarning("TMP Atlas Tool: TMP Settings asset not found in Resources; could not set default sprite asset.");
                return;
            }

            var so = new SerializedObject(settingsObj);
            var defaultSpriteProp = so.FindProperty("m_defaultSpriteAsset");
            if (defaultSpriteProp == null)
            {
                Debug.LogWarning("TMP Atlas Tool: TMP Settings has no 'm_defaultSpriteAsset' property; could not set default sprite asset.");
                return;
            }

            defaultSpriteProp.objectReferenceValue = spriteAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsObj);
            Debug.Log($"TMP Atlas Tool: Set TMP default sprite asset to '{spriteAsset.name}' via TMP Settings asset.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"TMP Atlas Tool: failed to set TMP default sprite asset automatically. {e.Message}");
        }
    }

    private static TMP_SpriteAsset CreateInitializedSpriteAsset(Texture2D atlasTexture)
    {
        // TMP 3.x has static CreateSpriteAsset(Texture2D). Prefer it to avoid null upgrade-path internals.
        var createMethod = typeof(TMP_SpriteAsset).GetMethod(
            "CreateSpriteAsset",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Texture2D) },
            null);

        if (createMethod != null)
        {
            var created = createMethod.Invoke(null, new object[] { atlasTexture }) as TMP_SpriteAsset;
            if (created != null)
                return created;
        }

        // Fallback for TMP variants where the static creator is unavailable.
        return ScriptableObject.CreateInstance<TMP_SpriteAsset>();
    }

    private static bool TryUpdateLookupTablesSafe(TMP_SpriteAsset spriteAsset)
    {
        if (spriteAsset == null) return false;
        try
        {
            spriteAsset.UpdateLookupTables();
            return true;
        }
        catch
        {
            // TMP 3.0.9 can throw in UpgradeSpriteAsset if legacy fields are null or malformed.
            RepairLegacyUpgradeFields(spriteAsset);
            try
            {
                spriteAsset.UpdateLookupTables();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static void PopulateLegacySpriteInfoFromAtlas(TMP_SpriteAsset spriteAsset, string atlasTexturePath)
    {
        if (spriteAsset == null || string.IsNullOrEmpty(atlasTexturePath)) return;

        var atlasSprites = AssetDatabase
            .LoadAllAssetsAtPath(atlasTexturePath)
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .ToList();

        var legacyList = new List<TMP_Sprite>(atlasSprites.Count);
        for (var i = 0; i < atlasSprites.Count; i++)
        {
            var s = atlasSprites[i];
            var r = s.rect;
            legacyList.Add(new TMP_Sprite
            {
                id = i,
                name = s.name,
                hashCode = TMP_TextUtilities.GetSimpleHashCode(s.name),
                unicode = PrivateUseAreaStart + i,
                x = r.x,
                y = r.y,
                width = r.width,
                height = r.height,
                xOffset = 0f,
                yOffset = r.height,
                xAdvance = r.width,
                scale = 1f,
                pivot = s.pivot,
                sprite = s
            });
        }

        // Force legacy-upgrade path with valid legacy data.
        spriteAsset.spriteInfoList = legacyList;
        var so = new SerializedObject(spriteAsset);
        var versionProp = so.FindProperty("m_Version");
        if (versionProp != null)
            versionProp.stringValue = string.Empty;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spriteAsset);
    }

    private static void RepairLegacyUpgradeFields(TMP_SpriteAsset spriteAsset)
    {
        if (spriteAsset == null) return;
        if (spriteAsset.spriteInfoList == null)
            spriteAsset.spriteInfoList = new List<TMP_Sprite>();
        var so = new SerializedObject(spriteAsset);
        var versionProp = so.FindProperty("m_Version");
        if (versionProp != null && versionProp.stringValue == null)
            versionProp.stringValue = string.Empty;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spriteAsset);
    }

    /// <summary>
    /// TMP sprite glyphs use <see cref="GlyphMetrics.horizontalBearingX"/> / <see cref="GlyphMetrics.horizontalBearingY"/>
    /// (inspector BX/BY). Adds the tool deltas to whatever values the bake produced for every glyph.
    /// </summary>
    private static void AddToAllSpriteGlyphBearings(TMP_SpriteAsset spriteAsset, int addBx, int addBy)
    {
        if (spriteAsset == null || (addBx == 0 && addBy == 0)) return;
        var glyphs = spriteAsset.spriteGlyphTable;
        if (glyphs == null) return;

        var dBx = (float)addBx;
        var dBy = (float)addBy;
        for (var i = 0; i < glyphs.Count; i++)
        {
            var g = glyphs[i];
            if (g == null) continue;
            var m = g.metrics;
            m.horizontalBearingX += dBx;
            m.horizontalBearingY += dBy;
            g.metrics = m;
        }
    }

    private static void FixCharacterGlyphLinks(TMP_SpriteAsset spriteAsset)
    {
        if (spriteAsset == null) return;
        var chars = spriteAsset.spriteCharacterTable;
        var glyphs = spriteAsset.spriteGlyphTable;
        if (chars == null || glyphs == null) return;

        var count = Mathf.Min(chars.Count, glyphs.Count);
        for (var i = 0; i < count; i++)
        {
            var ch = chars[i];
            var glyph = glyphs[i];
            if (ch == null || glyph == null) continue;
            ch.glyphIndex = glyph.index;
            ch.glyph = glyph;
            ch.textAsset = spriteAsset;
        }
    }

    private static void ConfigureAtlasImporter(
        string atlasPngPath,
        int atlasWidth,
        int atlasHeight,
        Rect[] packedRects,
        List<Sprite> sourceSprites)
    {
        var importer = AssetImporter.GetAtPath(atlasPngPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Bilinear;

        var metas = new SpriteMetaData[sourceSprites.Count];
        for (var i = 0; i < sourceSprites.Count; i++)
        {
            var packed = packedRects[i];
            var xMin = Mathf.FloorToInt(packed.xMin * atlasWidth);
            var yMin = Mathf.FloorToInt(packed.yMin * atlasHeight);
            var xMax = Mathf.CeilToInt(packed.xMax * atlasWidth);
            var yMax = Mathf.CeilToInt(packed.yMax * atlasHeight);

            xMin = Mathf.Clamp(xMin, 0, atlasWidth);
            yMin = Mathf.Clamp(yMin, 0, atlasHeight);
            xMax = Mathf.Clamp(xMax, xMin, atlasWidth);
            yMax = Mathf.Clamp(yMax, yMin, atlasHeight);

            var w = Mathf.Max(1, xMax - xMin);
            var h = Mathf.Max(1, yMax - yMin);

            metas[i] = new SpriteMetaData
            {
                name = sourceSprites[i].name,
                // TextureImporter spritesheet rect uses texture pixel space with bottom-left origin.
                rect = new Rect(xMin, yMin, w, h),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
        }

        importer.spritesheet = metas;
        // Avoid SaveAndReimport re-entrancy ("Existing reset not completed") while this texture is already
        // mid-import; write settings then request a fresh import explicitly.
        AssetDatabase.WriteImportSettingsIfDirty(atlasPngPath);
        AssetDatabase.ImportAsset(atlasPngPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private string ResolveOutputFolder()
    {
        if (outputFolder == null)
            return "Assets";

        var path = AssetDatabase.GetAssetPath(outputFolder);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets"))
            return null;
        return path;
    }

    private static void AddSpriteIfUnique(
        Sprite s,
        List<Sprite> uniqueSprites,
        HashSet<Sprite> seenSprites,
        HashSet<string> usedNames)
    {
        if (s == null) return;
        if (!seenSprites.Add(s)) return;

        var n = s.name;
        if (!usedNames.Add(n))
        {
            seenSprites.Remove(s);
            Debug.LogWarning(
                $"TMP Atlas Tool: duplicate sprite name '{n}' skipped — {AssetDatabase.GetAssetPath(s)} (keep first occurrence only).",
                s);
            return;
        }

        uniqueSprites.Add(s);
    }

    private static void CollectSpritesFromFolderRecursive(
        string folderPath,
        List<Sprite> uniqueSprites,
        HashSet<Sprite> seenSprites,
        HashSet<string> usedNames)
    {
        if (string.IsNullOrEmpty(folderPath) || !folderPath.StartsWith("Assets", StringComparison.Ordinal))
            return;

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (obj is Sprite sp)
                    AddSpriteIfUnique(sp, uniqueSprites, seenSprites, usedNames);
            }
        }
    }

    private static Texture2D ExtractSpriteTexture(Sprite sprite)
    {
        var srcTex = sprite.texture;
        var rect = sprite.textureRect;
        var readable = EnsureReadableCopy(srcTex);
        var pixels = readable.GetPixels(
            Mathf.RoundToInt(rect.x),
            Mathf.RoundToInt(rect.y),
            Mathf.RoundToInt(rect.width),
            Mathf.RoundToInt(rect.height));

        var tex = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false, false);
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        tex.name = sprite.name;
        return tex;
    }

    private static Texture2D EnsureReadableCopy(Texture2D source)
    {
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }
}
