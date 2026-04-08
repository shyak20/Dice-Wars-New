using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for combat UI icons: elements (attack/defense/fire/ice/nature), face actions, and status (buff/debuff) art.
/// </summary>
[CreateAssetMenu(fileName = "GameIconIndex", menuName = "DiceGame/UI/Game Icon Index")]
public class GameIconIndexSO : ScriptableObject
{
    [Header("Elements (pool / flyout rows by type)")]
    [SerializeField] private Sprite attack;
    [SerializeField] private Sprite defense;
    [SerializeField] private Sprite fire;
    [SerializeField] private Sprite ice;
    [SerializeField] private Sprite nature;

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

    public Sprite GetElementIcon(DieType type)
    {
        switch (type)
        {
            case DieType.Damage: return attack;
            case DieType.Armor: return defense;
            case DieType.Fire: return fire;
            case DieType.Ice: return ice;
            case DieType.Nature: return nature;
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
}
