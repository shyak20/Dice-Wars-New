using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StatusEffectBarUI : MonoBehaviour
{
    [SerializeField] private Transform iconContainer;
    [SerializeField] private GameObject statusEffectIconPrefab;
    [Header("Tooltip")]
    [Tooltip("Assign a HoverTooltipPanelUI prefab asset. Status icons instantiate/use this at runtime instead of scene lookup.")]
    [SerializeField] private HoverTooltipPanelUI statusTooltipPanelPrefab;

    private readonly Dictionary<string, StatusEffectIconUI> activeIcons = new();

    private StatusEffectManager trackedManager;
    private EnemyController trackedEnemy;
    private bool _refreshFrozen;
    private bool _refreshQueued;

    private void Awake()
    {
        if (iconContainer == null)
            Debug.LogError($"StatusEffectBarUI on '{gameObject.name}': iconContainer is not assigned!");
        if (statusEffectIconPrefab == null)
            Debug.LogError($"StatusEffectBarUI on '{gameObject.name}': statusEffectIconPrefab is not assigned!");
    }

    public void Bind(StatusEffectManager manager)
    {
        if (trackedManager != null)
            trackedManager.OnEffectsChanged -= QueueRefresh;

        trackedManager = manager;

        if (trackedManager == null)
        {
            Debug.LogError("StatusEffectBarUI: Bound to null StatusEffectManager!");
            return;
        }

        trackedManager.OnEffectsChanged += QueueRefresh;
        QueueRefresh();
    }

    public void BindEnemyStartingBuffs(EnemyController enemy)
    {
        trackedEnemy = enemy;
        QueueRefresh();
    }

    private void OnDestroy()
    {
        if (trackedManager != null)
            trackedManager.OnEffectsChanged -= QueueRefresh;
    }

    private void LateUpdate()
    {
        if (_refreshFrozen || !_refreshQueued)
            return;
        _refreshQueued = false;
        RefreshNow();
    }

    public void SetVisualRefreshFrozen(bool frozen)
    {
        _refreshFrozen = frozen;
        if (!_refreshFrozen && _refreshQueued)
        {
            _refreshQueued = false;
            RefreshNow();
        }
    }

    private void QueueRefresh()
    {
        _refreshQueued = true;
    }

    private void RefreshNow()
    {
        if (trackedManager == null)
            return;

        var nullIconKeys = activeIcons
            .Where(kvp => kvp.Value == null)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in nullIconKeys)
            activeIcons.Remove(key);

        var activeKeys = new HashSet<string>();

        foreach (var effect in trackedManager.Effects)
        {
            if (effect?.Definition == null)
                continue;
            var key = $"status:{effect.Definition.GetInstanceID()}";
            activeKeys.Add(key);
            var icon = GetOrCreateIcon(key);
            if (icon == null)
                continue;
            icon.Setup(effect);
        }

        if (trackedEnemy != null)
        {
            foreach (var pair in trackedEnemy.DamageResistances)
            {
                if (pair.Value <= 0f)
                    continue;

                var resistanceElement = pair.Key;
                var key = $"resistance:{resistanceElement}";
                activeKeys.Add(key);

                var icon = GetOrCreateIcon(key);
                if (icon == null)
                    continue;

                var iconSprite = GameIconCatalog.GetEnemyResistanceIcon(resistanceElement);
                var bgSprite = GameIconCatalog.GetEnemyResistanceBackground(resistanceElement);
                GameIconCatalog.TryGetEnemyResistanceTooltip(resistanceElement, out var title, out var description);

                if (string.IsNullOrWhiteSpace(title))
                    title = $"{resistanceElement} Resistance";
                var lessPercentText = Mathf.RoundToInt(pair.Value);
                var defaultDescription = $"Takes {lessPercentText}% less {resistanceElement.ToString().ToLowerInvariant()} damage.";
                if (string.IsNullOrWhiteSpace(description))
                    description = defaultDescription;
                else if (!description.Contains("%"))
                    description = $"{description}\n\n{defaultDescription}";

                icon.SetupCustom(iconSprite, title, description, bgSprite);
            }

            var listeners = trackedEnemy.ValueRolledListeners;
            for (var i = 0; i < listeners.Count; i++)
            {
                var listener = listeners[i];
                if (listener == null || listener.rolledValues == null || listener.rolledValues.Count == 0 || listener.icon == null)
                    continue;

                var key = $"roll-listener:{i}";
                activeKeys.Add(key);
                var icon = GetOrCreateIcon(key);
                if (icon == null)
                    continue;

                var title = string.IsNullOrWhiteSpace(listener.title) ? "Value Rolled Listener" : listener.title.Trim();
                var rolledValuesText = string.Join(", ", listener.rolledValues);
                var defaultDescription = $"If the player rolls {rolledValuesText}, this listener triggers immediately.";
                var description = string.IsNullOrWhiteSpace(listener.description) ? defaultDescription : listener.description.Trim();
                icon.SetupCustom(listener.icon, title, description);
            }
        }

        var staleKeys = activeIcons.Keys.Where(k => !activeKeys.Contains(k)).ToList();
        foreach (var key in staleKeys)
        {
            if (activeIcons.TryGetValue(key, out var iconUi) && iconUi != null)
                iconUi.gameObject.SetActive(false);
        }
    }

    private StatusEffectIconUI GetOrCreateIcon(string key)
    {
        if (activeIcons.TryGetValue(key, out var existingIcon) && existingIcon != null)
        {
            existingIcon.SetTooltipPanelPrefab(statusTooltipPanelPrefab);
            if (!existingIcon.gameObject.activeSelf)
                existingIcon.gameObject.SetActive(true);
            return existingIcon;
        }

        var iconObj = Instantiate(statusEffectIconPrefab, iconContainer);
        var iconUI = iconObj.GetComponent<StatusEffectIconUI>();
        if (iconUI == null)
        {
            Debug.LogError("StatusEffectBarUI: Prefab missing StatusEffectIconUI component!");
            Destroy(iconObj);
            return null;
        }

        iconUI.SetTooltipPanelPrefab(statusTooltipPanelPrefab);
        activeIcons[key] = iconUI;
        return iconUI;
    }
}
