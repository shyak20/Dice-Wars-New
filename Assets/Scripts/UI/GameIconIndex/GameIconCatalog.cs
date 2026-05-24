using UnityEngine;

/// <summary>
/// Runtime access to the active <see cref="GameIconIndexSO"/> (base attack/defence sprites, actions, statuses).
/// Registered from <see cref="RunManager"/> and/or <see cref="CombatManager"/>.
/// </summary>
public static class GameIconCatalog
{
    static GameIconIndexSO _active;

    public static GameIconIndexSO Active => _active;

    public static void Register(GameIconIndexSO index)
    {
        if (index == null) return;
        _active = index;
    }

    public static Sprite GetElementIcon(DieType type) =>
        _active != null ? _active.GetElementIcon(type) : null;

    public static Sprite GetActionIcon(ActionVisualId id) =>
        _active != null ? _active.GetActionIcon(id) : null;

    public static Sprite GetStatusIcon(StatusEffectSO effect) =>
        _active != null ? _active.GetStatusIcon(effect) : null;

    public static Sprite GetEnemyActionIcon(string actionTypeName) =>
        _active != null ? _active.GetEnemyActionIcon(actionTypeName) : null;

    public static Sprite GetEnemyActionBackground(string actionTypeName) =>
        _active != null ? _active.GetEnemyActionBackground(actionTypeName) : null;

    public static Sprite GetEnemyResistanceIcon(EnemyResistanceElement resistanceElement) =>
        _active != null ? _active.GetEnemyResistanceIcon(resistanceElement) : null;

    public static Sprite GetEnemyResistanceBackground(EnemyResistanceElement resistanceElement) =>
        _active != null ? _active.GetEnemyResistanceBackground(resistanceElement) : null;

    public static bool TryGetEnemyResistanceTooltip(EnemyResistanceElement resistanceElement, out string title, out string description)
    {
        title = null;
        description = null;
        return _active != null && _active.TryGetEnemyResistanceTooltip(resistanceElement, out title, out description);
    }

    public static Sprite GetElementBackground(DieType type) =>
        _active != null ? _active.GetElementBackground(type) : null;

    public static Sprite GetActionBackground(ActionVisualId id) =>
        _active != null ? _active.GetActionBackground(id) : null;

    public static bool TryGetActionTooltip(ActionVisualId id, out string title, out string description)
    {
        title = null;
        description = null;
        return _active != null && _active.TryGetActionTooltip(id, out title, out description);
    }

    public static Sprite TryGetPoolRowBackground(PoolRowKey key) =>
        _active != null ? _active.TryGetPoolRowBackground(key) : null;

    public static bool TryGetStatusTargetForPoolRow(PoolRowKey key, out StatusEffectTarget target)
    {
        target = default;
        return _active != null && _active.TryGetStatusTargetForPoolRow(key, out target);
    }

    public static Sprite GetMainAttributeIcon(MainAttributeIconId id) =>
        _active != null ? _active.GetMainAttributeIcon(id) : null;

    public static Sprite GetDieUnlockMainAttributeIcon(DieType dieType) =>
        _active != null ? _active.GetDieUnlockMainAttributeIcon(dieType) : null;
}
