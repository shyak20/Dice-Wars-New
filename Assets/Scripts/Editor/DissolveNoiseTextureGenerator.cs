using UnityEditor;
using UnityEngine;

/// <summary>Creates grayscale noise textures for DiceWars/Dissolve Lit (URP).</summary>
public static class DissolveNoiseTextureGenerator
{
    [MenuItem("Assets/Create/Dice Wars/Dissolve Noise Texture (256)", priority = 410)]
    private static void Create256()
    {
        CreateNoiseTexture(256, "DissolveNoise256");
    }

    [MenuItem("Assets/Create/Dice Wars/Dissolve Noise Texture (512)", priority = 411)]
    private static void Create512()
    {
        CreateNoiseTexture(512, "DissolveNoise512");
    }

    private static void CreateNoiseTexture(int size, string defaultName)
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Save Dissolve Noise Texture",
            defaultName,
            "png",
            "Choose where to save the dissolve noise texture.");

        if (string.IsNullOrEmpty(path))
            return;

        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                var n = SampleFbm(uv * 6f + new Vector2(2.71f, -5.3f));
                var b = (byte)Mathf.Clamp(Mathf.RoundToInt(n * 255f), 0, 255);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels32(pixels);
        texture.Apply();

        var png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        System.IO.File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }

    private static float SampleFbm(Vector2 p)
    {
        var sum = 0f;
        var amp = 0.52f;
        var q = p;
        for (var i = 0; i < 3; i++)
        {
            sum += ValueNoise(q) * amp;
            q *= 2.07f;
            amp *= 0.5f;
        }

        return Mathf.Clamp01(sum * 0.97f + 0.015f);
    }

    private static float ValueNoise(Vector2 uv)
    {
        var i = new Vector2(Mathf.Floor(uv.x), Mathf.Floor(uv.y));
        var f = new Vector2(uv.x - i.x, uv.y - i.y);
        f.x = f.x * f.x * (3f - 2f * f.x);
        f.y = f.y * f.y * (3f - 2f * f.y);

        var a = Hash21(i);
        var b = Hash21(i + Vector2.right);
        var c = Hash21(i + Vector2.up);
        var d = Hash21(i + Vector2.one);
        return Mathf.Lerp(Mathf.Lerp(a, b, f.x), Mathf.Lerp(c, d, f.x), f.y);
    }

    private static float Hash21(Vector2 p)
    {
        p = new Vector2(
            p.x * 234.34f - Mathf.Floor(p.x * 234.34f),
            p.y * 345.45f - Mathf.Floor(p.y * 345.45f));
        var dot = p.x * p.y + p.x + p.y + 34.345f;
        return dot - Mathf.Floor(dot);
    }
}
