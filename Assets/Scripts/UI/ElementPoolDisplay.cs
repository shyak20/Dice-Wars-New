using System;
using System.Collections.Generic;
using UnityEngine;

public class ElementPoolDisplay : MonoBehaviour
{
    [Tooltip("When enabled, pool icons only change on flyout landing + full resync events. Wire DiceRollOutcomeFlyoutController and assign it in the scene.")]
    [SerializeField] private bool incrementPoolIconsWithFlyouts;

    [SerializeField] private ElementPoolIcon damageIcon;
    [SerializeField] private ElementPoolIcon armorIcon;
    [SerializeField] private ElementPoolIcon fireIcon;
    [SerializeField] private ElementPoolIcon iceIcon;
    [SerializeField] private ElementPoolIcon natureIcon;

    private Dictionary<DieType, ElementPoolIcon> iconMap;
    private Dictionary<DieType, int> displayedPools;

    private void Awake()
    {
        iconMap = new Dictionary<DieType, ElementPoolIcon>
        {
            { DieType.Damage, damageIcon },
            { DieType.Armor, armorIcon },
            { DieType.Fire, fireIcon },
            { DieType.Ice, iceIcon },
            { DieType.Nature, natureIcon },
        };

        displayedPools = new Dictionary<DieType, int>();
        foreach (DieType type in Enum.GetValues(typeof(DieType)))
            displayedPools[type] = 0;

        foreach (var kvp in iconMap)
        {
            if (kvp.Value == null)
            {
                Debug.LogError($"ElementPoolDisplay: {kvp.Key} icon is not assigned!");
            }
            else
            {
                kvp.Value.gameObject.SetActive(false);
            }
        }
    }

    private void OnEnable()
    {
        CombatEvents.OnPoolIconsFullResync += ApplyFullPoolSync;
        if (!incrementPoolIconsWithFlyouts)
            CombatEvents.OnPoolsUpdated += ApplyFullPoolSync;
    }

    private void OnDisable()
    {
        CombatEvents.OnPoolIconsFullResync -= ApplyFullPoolSync;
        if (!incrementPoolIconsWithFlyouts)
            CombatEvents.OnPoolsUpdated -= ApplyFullPoolSync;
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

    /// <summary>Sprite used in the pool bar for this type (same art as <see cref="ElementPoolIcon"/>).</summary>
    public Sprite GetPoolTypeSprite(DieType type)
    {
        if (iconMap == null || !iconMap.TryGetValue(type, out var icon) || icon == null)
            return null;
        return icon.PoolTypeSprite;
    }

    /// <summary>Called when a flyout reaches the pool bar; increments the visible total for that type.</summary>
    public void ApplyPoolDelta(DieType type, int delta)
    {
        if (delta == 0) return;
        if (!displayedPools.ContainsKey(type))
            displayedPools[type] = 0;
        displayedPools[type] += delta;
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
        if (!iconMap.TryGetValue(type, out var icon) || icon == null) return;

        int v = displayedPools.TryGetValue(type, out var stored) ? stored : 0;
        bool visible = v >= 1;
        icon.gameObject.SetActive(visible);
        if (visible)
            icon.SetValue(v);
    }

    /// <summary>Show pre-jackpot values and ×multiplier above each visible pool icon.</summary>
    public void BeginJackpotPresentation(int multiplier, Dictionary<DieType, int> valuesBefore)
    {
        if (valuesBefore == null) return;

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
        foreach (var kvp in iconMap)
            kvp.Value?.HideJackpotMultiplierBadge();

        if (valuesAfter != null)
            ApplyFullPoolSync(valuesAfter);
    }
}
