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

    [Tooltip("Optional panel/frame behind attack/damage pool icons.")]
    [SerializeField] private Sprite attackBackground;

    [Tooltip("Optional panel/frame behind armour pool icons.")]
    [SerializeField] private Sprite defenceBackground;

    [Header("Actions (keys match ActionVisualId on each action class)")]
    [SerializeField] private List<ActionIconEntry> actionIcons = new List<ActionIconEntry>();

    [Header("Status effects (buffs & debuffs)")]
    [SerializeField] private List<StatusEffectIconEntry> statusEffectIcons = new List<StatusEffectIconEntry>();

    [Serializable]
    public struct ActionIconEntry
    {
        public ActionVisualId id;
        public Sprite sprite;
        [Tooltip("Optional. Used by GetActionBackground; pool rows resolve backgrounds via die types or status entries (effect name = PoolRowKey).")]
        public Sprite background;
    }

    [Serializable]
    public struct StatusEffectIconEntry
    {
        public StatusEffectSO effect;
        public Sprite icon;
        [Tooltip("Shown behind the status pool icon; keyed by effect asset name as PoolRowKey.")]
        public Sprite background;
    }

    [Serializable]
    public struct NamedIconEntry
    {
        public string key;
        public Sprite sprite;
    }

    readonly Dictionary<ActionVisualId, Sprite> _actionLookup = new Dictionary<ActionVisualId, Sprite>();
    readonly Dictionary<StatusEffectSO, Sprite> _statusLookup = new Dictionary<StatusEffectSO, Sprite>();
    readonly Dictionary<ActionVisualId, Sprite> _actionBackgroundLookup = new Dictionary<ActionVisualId, Sprite>();
    readonly Dictionary<StatusEffectSO, Sprite> _statusBackgroundLookup = new Dictionary<StatusEffectSO, Sprite>();
    readonly Dictionary<string, Sprite> _poolRowBackgroundByStableId = new Dictionary<string, Sprite>(StringComparer.Ordinal);

    private void OnEnable() => RebuildLookups();

    private void OnValidate() => RebuildLookups();

    public void RebuildLookups()
    {
        _actionLookup.Clear();
        _actionBackgroundLookup.Clear();
        _poolRowBackgroundByStableId.Clear();
        foreach (var e in actionIcons)
        {
            if (e.id != ActionVisualId.None && e.sprite != null)
                _actionLookup[e.id] = e.sprite;
            if (e.id != ActionVisualId.None && e.background != null)
                _actionBackgroundLookup[e.id] = e.background;
        }

        _statusLookup.Clear();
        _statusBackgroundLookup.Clear();
        foreach (var e in statusEffectIcons)
        {
            if (e.effect == null) continue;
            if (e.icon != null)
                _statusLookup[e.effect] = e.icon;
            if (e.background != null)
            {
                _statusBackgroundLookup[e.effect] = e.background;
                _poolRowBackgroundByStableId[e.effect.name] = e.background;
            }
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

    /// <summary>Background art for <see cref="DieType.Damage"/> / <see cref="DieType.Armor"/> pool rows.</summary>
    public Sprite GetElementBackground(DieType type)
    {
        switch (type)
        {
            case DieType.Damage: return attackBackground;
            case DieType.Armor: return defenceBackground;
            default: return null;
        }
    }

    public Sprite GetActionBackground(ActionVisualId id)
    {
        if (id == ActionVisualId.None) return null;
        if (_actionBackgroundLookup.Count == 0 && actionIcons.Count > 0)
            RebuildLookups();
        return _actionBackgroundLookup.TryGetValue(id, out var s) ? s : null;
    }

    public Sprite GetStatusBackground(StatusEffectSO effect)
    {
        if (effect == null) return null;
        if (_statusBackgroundLookup.Count == 0 && statusEffectIcons.Count > 0)
            RebuildLookups();
        return _statusBackgroundLookup.TryGetValue(effect, out var s) ? s : null;
    }

    /// <summary>
    /// Resolves a frame behind <see cref="StoredActionsPoolIcon"/> for this pool row (die types or status entry keyed by <c>effect.name</c> matching <see cref="PoolRowKey.StableId"/>).
    /// </summary>
    public Sprite TryGetPoolRowBackground(PoolRowKey key)
    {
        if (PoolRowKey.TryGetDieType(key, out var dt))
            return GetElementBackground(dt);
        if (_poolRowBackgroundByStableId.Count == 0 && (actionIcons.Count > 0 || statusEffectIcons.Count > 0))
            RebuildLookups();
        return _poolRowBackgroundByStableId.TryGetValue(key.StableId, out var bg) ? bg : null;
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

        if (attackBackground != null)
            entries.Add(new NamedIconEntry { key = "BaseAction.AttackBackground", sprite = attackBackground });
        if (defenceBackground != null)
            entries.Add(new NamedIconEntry { key = "BaseAction.DefenceBackground", sprite = defenceBackground });

        foreach (var action in actionIcons)
        {
            entries.Add(new NamedIconEntry
            {
                key = $"Action.{action.id}",
                sprite = action.sprite
            });
            if (action.background != null)
                entries.Add(new NamedIconEntry { key = $"Action.{action.id}.Background", sprite = action.background });
        }

        foreach (var status in statusEffectIcons)
        {
            entries.Add(new NamedIconEntry
            {
                key = status.effect != null ? $"Status.{status.effect.name}" : "Status.(null)",
                sprite = status.icon
            });
            if (status.background != null && status.effect != null)
                entries.Add(new NamedIconEntry { key = $"Status.{status.effect.name}.Background", sprite = status.background });
        }

        return entries;
    }
}
