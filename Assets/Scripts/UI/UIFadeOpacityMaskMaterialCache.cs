using System.Collections.Generic;
using UnityEngine;

/// <summary>Caches soft-mask material instances (mirrors Unity UI stencil material caching).</summary>
static class UIFadeOpacityMaskMaterialCache
{
    sealed class CacheEntry
    {
        public Material material;
        public int refCount;
    }

    static readonly Dictionary<(int baseId, int maskId), CacheEntry> Entries = new();
    static readonly Dictionary<int, HashSet<Material>> MaterialsByMaskId = new();

    public static Material Acquire(Material baseMaterial, UIFadeOpacityMask mask, Shader maskableShader)
    {
        if (baseMaterial == null || mask == null || maskableShader == null)
            return baseMaterial;

        var key = (baseMaterial.GetInstanceID(), mask.GetInstanceID());
        if (!Entries.TryGetValue(key, out var entry))
        {
            var mat = new Material(maskableShader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = $"{baseMaterial.name} (FadeOpacityMask)",
            };
            CopyCommonProperties(baseMaterial, mat);
            entry = new CacheEntry { material = mat, refCount = 0 };
            Entries[key] = entry;
        }

        entry.refCount++;
        RegisterDrivenMaterial(mask, entry.material);
        mask.PushMaskProperties(entry.material);
        return entry.material;
    }

    public static void ApplyMaskPropertiesToAll(UIFadeOpacityMask mask)
    {
        if (mask == null)
            return;
        if (!MaterialsByMaskId.TryGetValue(mask.GetInstanceID(), out var set))
            return;
        foreach (var mat in set)
            mask.PushMaskProperties(mat);
    }

    public static void Release(Material modifiedMaterial, Material baseMaterial, UIFadeOpacityMask mask)
    {
        if (modifiedMaterial == null || baseMaterial == null || mask == null)
            return;

        var key = (baseMaterial.GetInstanceID(), mask.GetInstanceID());
        if (!Entries.TryGetValue(key, out var entry) || entry.material != modifiedMaterial)
            return;

        entry.refCount--;
        if (entry.refCount > 0)
            return;

        if (entry.material != null)
        {
            UnregisterDrivenMaterial(mask, entry.material);
            Object.DestroyImmediate(entry.material);
        }

        Entries.Remove(key);
    }

    static void RegisterDrivenMaterial(UIFadeOpacityMask mask, Material mat)
    {
        var id = mask.GetInstanceID();
        if (!MaterialsByMaskId.TryGetValue(id, out var set))
        {
            set = new HashSet<Material>();
            MaterialsByMaskId[id] = set;
        }

        set.Add(mat);
    }

    static void UnregisterDrivenMaterial(UIFadeOpacityMask mask, Material mat)
    {
        var id = mask.GetInstanceID();
        if (!MaterialsByMaskId.TryGetValue(id, out var set))
            return;
        set.Remove(mat);
        if (set.Count == 0)
            MaterialsByMaskId.Remove(id);
    }

    static void CopyCommonProperties(Material from, Material to)
    {
        if (from.HasProperty("_MainTex") && to.HasProperty("_MainTex"))
            to.SetTexture("_MainTex", from.GetTexture("_MainTex"));
        if (from.HasProperty("_MainTex_ST") && to.HasProperty("_MainTex_ST"))
            to.SetVector("_MainTex_ST", from.GetVector("_MainTex_ST"));
        if (from.HasProperty("_Color") && to.HasProperty("_Color"))
            to.SetColor("_Color", from.GetColor("_Color"));
        if (from.HasProperty("_TextureSampleAdd") && to.HasProperty("_TextureSampleAdd"))
            to.SetVector("_TextureSampleAdd", from.GetVector("_TextureSampleAdd"));
    }
}
