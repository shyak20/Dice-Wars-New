using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds one <see cref="ElementPoolIcon"/> per <see cref="DieType"/> from a prefab and assigns sprites from <see cref="GameIconCatalog"/>.
/// </summary>
public class ElementPoolDisplay : MonoBehaviour
{
    [Tooltip("When enabled, pool icons only change on flyout landing + full resync events. Wire DiceRollOutcomeFlyoutController and assign it in the scene.")]
    [SerializeField] private bool incrementPoolIconsWithFlyouts;

    [Header("Dynamic pool icons")]
    [Tooltip("Parent for instantiated icons (defaults to this transform). Use a layout group here for spacing.")]
    [SerializeField] private RectTransform iconContainer;
    [SerializeField] private ElementPoolIcon poolIconPrefab;

    private Dictionary<DieType, ElementPoolIcon> iconMap;
    private Dictionary<DieType, int> displayedPools;
    private readonly Dictionary<DieType, Sprite> runtimeTypeIcons = new Dictionary<DieType, Sprite>();

    private void Awake()
    {
        if (poolIconPrefab == null)
        {
            Debug.LogError($"ElementPoolDisplay on '{name}': assign poolIconPrefab (ElementPoolIcon).");
            return;
        }

        var parent = iconContainer != null ? iconContainer : (RectTransform)transform;
        iconMap = new Dictionary<DieType, ElementPoolIcon>();

        foreach (DieType type in Enum.GetValues(typeof(DieType)))
        {
            var inst = Instantiate(poolIconPrefab, parent);
            inst.name = $"PoolIcon_{type}";
            var rt = inst.transform as RectTransform;
            if (rt != null)
                rt.localScale = Vector3.one;

            var comp = inst.GetComponent<ElementPoolIcon>();
            if (comp == null)
            {
                Debug.LogError($"ElementPoolDisplay: poolIconPrefab must have an ElementPoolIcon component.");
                Destroy(inst.gameObject);
                continue;
            }

            iconMap[type] = comp;
            comp.ConfigureType(type);
            comp.gameObject.SetActive(false);
        }

        displayedPools = new Dictionary<DieType, int>();
        foreach (DieType type in Enum.GetValues(typeof(DieType)))
            displayedPools[type] = 0;
    }

    private void OnEnable()
    {
        CombatEvents.OnPoolIconsFullResync += ApplyFullPoolSync;
        CombatEvents.OnElementPoolRuntimeIconsClear += ClearRuntimeTypeIcons;
        CombatEvents.OnRuntimePoolIconForType += OnRuntimePoolIconForType;
        if (!incrementPoolIconsWithFlyouts)
            CombatEvents.OnPoolsUpdated += ApplyFullPoolSync;
    }

    private void OnDisable()
    {
        CombatEvents.OnPoolIconsFullResync -= ApplyFullPoolSync;
        CombatEvents.OnElementPoolRuntimeIconsClear -= ClearRuntimeTypeIcons;
        CombatEvents.OnRuntimePoolIconForType -= OnRuntimePoolIconForType;
        if (!incrementPoolIconsWithFlyouts)
            CombatEvents.OnPoolsUpdated -= ApplyFullPoolSync;
    }

    private void OnRuntimePoolIconForType(DieType type, Sprite sprite)
    {
        if (sprite == null) return;
        runtimeTypeIcons[type] = sprite;
        RefreshIcon(type);
    }

    public bool UsesFlyoutIncrementMode => incrementPoolIconsWithFlyouts;

    public RectTransform GetFlyTargetRect(DieType type)
    {
        if (iconMap == null || !iconMap.TryGetValue(type, out var icon) || icon == null)
        {
            Debug.LogError($"ElementPoolDisplay: No icon / fly target for {type}.");
            return null;
        }

        return icon.FlyTargetRect;
    }

    /// <summary>Sprite for flyout row + pool bar: runtime override, then <see cref="GameIconCatalog"/>.</summary>
    public Sprite GetPoolTypeSprite(DieType type)
    {
        if (runtimeTypeIcons.TryGetValue(type, out var rt) && rt != null)
            return rt;
        return GameIconCatalog.GetElementIcon(type);
    }

    /// <summary>Called when a flyout reaches the pool bar; increments the visible total for that type.</summary>
    public void ApplyPoolDelta(DieType type, int delta, Sprite lineIconOverride = null)
    {
        if (delta == 0) return;
        if (!displayedPools.ContainsKey(type))
            displayedPools[type] = 0;
        displayedPools[type] += delta;
        if (lineIconOverride != null)
            runtimeTypeIcons[type] = lineIconOverride;
        RefreshIcon(type);
    }

    public void ClearRuntimeTypeIcons()
    {
        runtimeTypeIcons.Clear();
        foreach (DieType type in Enum.GetValues(typeof(DieType)))
            RefreshIcon(type);
    }

    private void ApplyFullPoolSync(Dictionary<DieType, int> pools)
    {
        foreach (var kvp in pools)
            displayedPools[kvp.Key] = kvp.Value;

        foreach (DieType type in Enum.GetValues(typeof(DieType)))
            RefreshIcon(type);
    }

    private void RefreshIcon(DieType type)
    {
        if (iconMap == null || !iconMap.TryGetValue(type, out var poolIcon) || poolIcon == null) return;

        int v = displayedPools.TryGetValue(type, out var stored) ? stored : 0;
        if (v < 1)
        {
            runtimeTypeIcons.Remove(type);
            poolIcon.gameObject.SetActive(false);
            return;
        }

        poolIcon.gameObject.SetActive(true);
        poolIcon.SetPoolSprite(GetPoolTypeSprite(type));
        poolIcon.SetValue(v);
    }

    /// <summary>Show pre-jackpot values and ×multiplier above each visible pool icon.</summary>
    public void BeginJackpotPresentation(int multiplier, Dictionary<DieType, int> valuesBefore)
    {
        if (valuesBefore == null || iconMap == null) return;

        foreach (var kvp in valuesBefore)
            displayedPools[kvp.Key] = kvp.Value;

        foreach (DieType type in Enum.GetValues(typeof(DieType)))
            RefreshIcon(type);

        foreach (DieType type in Enum.GetValues(typeof(DieType)))
        {
            if (!valuesBefore.TryGetValue(type, out int v) || v < 1) continue;
            if (!iconMap.TryGetValue(type, out var icon) || icon == null) continue;
            icon.ShowJackpotMultiplierBadge(multiplier);
        }
    }

    /// <summary>Hide badges and apply post-jackpot pool totals to the bar.</summary>
    public void FinishJackpotPresentation(Dictionary<DieType, int> valuesAfter)
    {
        if (iconMap == null) return;
        foreach (var kvp in iconMap)
            kvp.Value?.HideJackpotMultiplierBadge();

        if (valuesAfter != null)
            ApplyFullPoolSync(valuesAfter);
    }
}
