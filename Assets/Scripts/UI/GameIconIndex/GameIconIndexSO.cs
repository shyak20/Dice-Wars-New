using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Combat UI icons: base actions (attack/defence), face <see cref="ActionVisualId"/>, and status effect art.
/// </summary>
[CreateAssetMenu(fileName = "GameIconIndex", menuName = "DiceGame/UI/Game Icon Index")]
public class GameIconIndexSO : ScriptableObject
{
    [Header("Base Actions")]
    [Tooltip("Icon for stored physical attack / damage rows.")]
    [SerializeField] private Sprite attack;

    [Tooltip("Icon for stored armour / defence rows.")]
    [FormerlySerializedAs("defense")]
    [SerializeField] private Sprite defence;

    [Header("Actions (keys match ActionVisualId on each action class)")]
    [SerializeField] private List<ActionIconEntry> actionIcons = new List<ActionIconEntry>();

    [Header("Status effects (buffs & debuffs)")]
    [SerializeField] private List<StatusEffectIconEntry> statusEffectIcons = new List<StatusEffectIconEntry>();

    [Serializable]
    public struct ActionIconEntry
    {
        public ActionVisualId id;
        public Sprite sprite;
    }

    [Serializable]
    public struct StatusEffectIconEntry
    {
        public StatusEffectSO effect;
        public Sprite icon;
    }

    [Serializable]
    public struct NamedIconEntry
    {
        public string key;
        public Sprite sprite;
    }

    readonly Dictionary<ActionVisualId, Sprite> _actionLookup = new Dictionary<ActionVisualId, Sprite>();
    readonly Dictionary<StatusEffectSO, Sprite> _statusLookup = new Dictionary<StatusEffectSO, Sprite>();

    private void OnEnable() => RebuildLookups();

    private void OnValidate() => RebuildLookups();

    public void RebuildLookups()
    {
        _actionLookup.Clear();
        foreach (var e in actionIcons)
        {
            if (e.id == ActionVisualId.None || e.sprite == null) continue;
            _actionLookup[e.id] = e.sprite;
        }

        _statusLookup.Clear();
        foreach (var e in statusEffectIcons)
        {
            if (e.effect == null || e.icon == null) continue;
            _statusLookup[e.effect] = e.icon;
        }
    }

    /// <summary>Icons for <see cref="DieType.Damage"/> and <see cref="DieType.Armor"/> rows only.</summary>
    public Sprite GetElementIcon(DieType type)
    {
        switch (type)
        {
            case DieType.Damage: return attack;
            case DieType.Armor: return defence;
            default: return null;
        }
    }

    public Sprite GetActionIcon(ActionVisualId id)
    {
        if (id == ActionVisualId.None) return null;
        if (_actionLookup.Count == 0 && actionIcons.Count > 0)
            RebuildLookups();
        return _actionLookup.TryGetValue(id, out var s) ? s : null;
    }

    public Sprite GetStatusIcon(StatusEffectSO effect)
    {
        if (effect == null) return null;
        if (_statusLookup.Count == 0 && statusEffectIcons.Count > 0)
            RebuildLookups();
        return _statusLookup.TryGetValue(effect, out var s) ? s : null;
    }

    /// <summary>
    /// Export helper for editor tooling (atlas generation, audits).
    /// </summary>
    public List<NamedIconEntry> GetAllIconEntries()
    {
        var entries = new List<NamedIconEntry>
        {
            new NamedIconEntry { key = "BaseAction.Attack", sprite = attack },
            new NamedIconEntry { key = "BaseAction.Defence", sprite = defence },
        };

        foreach (var action in actionIcons)
        {
            entries.Add(new NamedIconEntry
            {
                key = $"Action.{action.id}",
                sprite = action.sprite
            });
        }

        foreach (var status in statusEffectIcons)
        {
            entries.Add(new NamedIconEntry
            {
                key = status.effect != null ? $"Status.{status.effect.name}" : "Status.(null)",
                sprite = status.icon
            });
        }

        return entries;
    }
}
