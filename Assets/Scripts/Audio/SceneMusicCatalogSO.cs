using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bootstrap table: maps built scene <see cref="Scene.name"/> (no path) to music used when transitioning
/// <i>before</i> that scene is loaded (first visit). After a scene loads, <see cref="SceneMusicTarget"/> overrides these entries.
/// </summary>
[CreateAssetMenu(fileName = "SceneMusicCatalog", menuName = "DiceGame/Audio/Scene Music Catalog")]
public sealed class SceneMusicCatalogSO : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("Must match Unity scene name (same as Scene.name after load).")]
        public string sceneName;

        public AudioClip clip;

        [Min(0.01f)] public float crossfadeDurationSeconds = 1.5f;

        public bool loop = true;
    }

    public List<Entry> entries = new List<Entry>();

    /// <summary>Returns first matching entry for <paramref name="sceneName"/> (trimmed, ordinal ignore case).</summary>
    public bool TryGet(string sceneName, out Entry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(sceneName) || entries == null)
            return false;

        var key = sceneName.Trim();
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || string.IsNullOrWhiteSpace(e.sceneName))
                continue;
            if (string.Equals(e.sceneName.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                entry = e;
                return true;
            }
        }

        return false;
    }
}
