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
    [Tooltip("Icon for stored physical attack / damage pool rows and face type chip.")]
    [SerializeField] private Sprite attack;

    [Tooltip("Icon for stored armour / defence pool rows and face type chip.")]
    [FormerlySerializedAs("defense")]
    [SerializeField] private Sprite defence;

    [Tooltip("Panel behind attack/damage pool row icons (not die tooltips).")]
    [SerializeField] private Sprite attackBackground;

    [Tooltip("Panel behind armour pool row icons (not die tooltips).")]
    [SerializeField] private Sprite defenceBackground;

    [Header("Actions (keys match ActionVisualId on each action class)")]
    [SerializeField] private List<ActionIconEntry> actionIcons = new List<ActionIconEntry>();
    [Header("Enemy Actions (keys match action class names, e.g. HealAction)")]
    [SerializeField] private List<EnemyActionIconEntry> enemyActionIcons = new List<EnemyActionIconEntry>();

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
    public struct EnemyActionIconEntry
    {
        [Tooltip("Selected from all IGameAction types in inspector (stored as type name).")]
        public string actionTypeName;
        public Sprite icon;
        public Sprite background;
    }

    [Serializable]
    public struct StatusEffectIconEntry
    {
        public StatusEffectSO effect;
        public Sprite icon;

        [Tooltip("Behind the icon in the stored-actions pool row (PoolRowKey stable id = effect asset name).")]
        public Sprite poolRowBackground;
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
    readonly Dictionary<string, Sprite> _enemyActionIconLookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    readonly Dictionary<string, Sprite> _enemyActionBackgroundLookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    readonly Dictionary<string, Sprite> _poolRowBackgroundByStableId = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    readonly Dictionary<string, StatusEffectTarget> _statusTargetByPoolRowStableId = new Dictionary<string, StatusEffectTarget>(StringComparer.Ordinal);

    private void OnEnable() => RebuildLookups();

    private void OnValidate() => RebuildLookups();

    public void RebuildLookups()
    {
        _actionLookup.Clear();
        _actionBackgroundLookup.Clear();
        _enemyActionIconLookup.Clear();
        _enemyActionBackgroundLookup.Clear();
        _poolRowBackgroundByStableId.Clear();
        _statusTargetByPoolRowStableId.Clear();
        foreach (var e in actionIcons)
        {
            if (e.id != ActionVisualId.None && e.sprite != null)
                _actionLookup[e.id] = e.sprite;
            if (e.id != ActionVisualId.None && e.background != null)
                _actionBackgroundLookup[e.id] = e.background;
        }
        foreach (var e in enemyActionIcons)
        {
            if (string.IsNullOrWhiteSpace(e.actionTypeName)) continue;
            var key = e.actionTypeName.Trim();
            if (e.icon != null)
                _enemyActionIconLookup[key] = e.icon;
            if (e.background != null)
                _enemyActionBackgroundLookup[key] = e.background;

            // Backward/forward compatibility: if key is a full type name, also map short type name.
            var dot = key.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < key.Length)
            {
                var shortName = key.Substring(dot + 1);
                if (e.icon != null && !_enemyActionIconLookup.ContainsKey(shortName))
                    _enemyActionIconLookup[shortName] = e.icon;
                if (e.background != null && !_enemyActionBackgroundLookup.ContainsKey(shortName))
                    _enemyActionBackgroundLookup[shortName] = e.background;
            }
        }

        _statusLookup.Clear();
        foreach (var e in statusEffectIcons)
        {
            if (e.effect == null) continue;
            if (e.icon != null)
                _statusLookup[e.effect] = e.icon;
            _statusTargetByPoolRowStableId[e.effect.name] = e.effect.target;
            if (e.poolRowBackground != null)
                _poolRowBackgroundByStableId[e.effect.name] = e.poolRowBackground;
        }
    }

    /// <summary>Icons for <see cref="DieType.Damage"/> / <see cref="DieType.Armor"/> pool rows and face chips; element types have no base icon here.</summary>
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

    /// <summary>Background for stored-actions pool rows only (<see cref="TryGetPoolRowBackground"/>).</summary>
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

    public Sprite GetEnemyActionIcon(string actionTypeName)
    {
        if (string.IsNullOrWhiteSpace(actionTypeName)) return null;
        if (_enemyActionIconLookup.Count == 0 && enemyActionIcons.Count > 0)
            RebuildLookups();
        return _enemyActionIconLookup.TryGetValue(actionTypeName.Trim(), out var s) ? s : null;
    }

    public Sprite GetEnemyActionBackground(string actionTypeName)
    {
        if (string.IsNullOrWhiteSpace(actionTypeName)) return null;
        if (_enemyActionBackgroundLookup.Count == 0 && enemyActionIcons.Count > 0)
            RebuildLookups();
        return _enemyActionBackgroundLookup.TryGetValue(actionTypeName.Trim(), out var s) ? s : null;
    }

    /// <summary>
    /// Resolves a frame behind <see cref="StoredActionsPoolIcon"/> for this pool row
    /// (<see cref="DieType"/> rows or per-status <see cref="StatusEffectIconEntry.poolRowBackground"/> keyed by <c>effect.name</c> as <see cref="PoolRowKey.StableId"/>).
    /// </summary>
    public Sprite TryGetPoolRowBackground(PoolRowKey key)
    {
        if (PoolRowKey.TryGetDieType(key, out var dt))
            return GetElementBackground(dt);
        if (_poolRowBackgroundByStableId.Count == 0 && (actionIcons.Count > 0 || statusEffectIcons.Count > 0))
            RebuildLookups();
        return _poolRowBackgroundByStableId.TryGetValue(key.StableId, out var bg) ? bg : null;
    }

    /// <summary>Resolves whether a pool row key belongs to a known status effect and returns its target side.</summary>
    public bool TryGetStatusTargetForPoolRow(PoolRowKey key, out StatusEffectTarget target)
    {
        if (_statusTargetByPoolRowStableId.Count == 0 && statusEffectIcons.Count > 0)
            RebuildLookups();
        return _statusTargetByPoolRowStableId.TryGetValue(key.StableId, out target);
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
        foreach (var enemyAction in enemyActionIcons)
        {
            if (string.IsNullOrWhiteSpace(enemyAction.actionTypeName))
                continue;
            entries.Add(new NamedIconEntry { key = $"EnemyAction.{enemyAction.actionTypeName}.Icon", sprite = enemyAction.icon });
            if (enemyAction.background != null)
                entries.Add(new NamedIconEntry { key = $"EnemyAction.{enemyAction.actionTypeName}.Background", sprite = enemyAction.background });
        }

        foreach (var status in statusEffectIcons)
        {
            entries.Add(new NamedIconEntry
            {
                key = status.effect != null ? $"Status.{status.effect.name}" : "Status.(null)",
                sprite = status.icon
            });
            if (status.poolRowBackground != null && status.effect != null)
                entries.Add(new NamedIconEntry { key = $"Status.{status.effect.name}.PoolRowBackground", sprite = status.poolRowBackground });
        }

        return entries;
    }
}
