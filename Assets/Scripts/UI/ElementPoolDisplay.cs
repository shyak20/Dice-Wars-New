using System;
using System.Collections.Generic;
using UnityEngine;

public class ElementPoolDisplay : MonoBehaviour
{
    [SerializeField] private ElementPoolIcon damageIcon;
    [SerializeField] private ElementPoolIcon armorIcon;
    [SerializeField] private ElementPoolIcon fireIcon;
    [SerializeField] private ElementPoolIcon iceIcon;
    [SerializeField] private ElementPoolIcon natureIcon;

    private Dictionary<DieType, ElementPoolIcon> iconMap;

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

        foreach (var kvp in iconMap)
        {
            if (kvp.Value == null)
            {
                Debug.LogError($"ElementPoolDisplay: {kvp.Key} icon is not assigned!");
            }
            else
            {
                // Start with all icons hidden
                kvp.Value.gameObject.SetActive(false);
            }
        }
    }

    private void OnEnable()
    {
        CombatEvents.OnPoolsUpdated += UpdatePools;
    }

    private void OnDisable()
    {
        CombatEvents.OnPoolsUpdated -= UpdatePools;
    }

    private void UpdatePools(Dictionary<DieType, int> pools)
    {
        foreach (var kvp in pools)
        {
            if (iconMap.TryGetValue(kvp.Key, out var icon))
            {
                // Check if the element has at least 1 value
                bool shouldBeVisible = kvp.Value >= 1;

                // Toggle the GameObject visibility
                icon.gameObject.SetActive(shouldBeVisible);

                // Only update the text value if the icon is visible
                if (shouldBeVisible)
                {
                    icon.SetValue(kvp.Value);
                }
            }
        }
    }
}