using System;
using System.Collections.Generic;
using UnityEngine;

public class ElementPoolDisplay : MonoBehaviour
{
    [SerializeField] private ElementPoolIcon shadowIcon;
    [SerializeField] private ElementPoolIcon defenseIcon;
    [SerializeField] private ElementPoolIcon fireIcon;
    [SerializeField] private ElementPoolIcon iceIcon;
    [SerializeField] private ElementPoolIcon natureIcon;

    private Dictionary<DieType, ElementPoolIcon> iconMap;

    private void Awake()
    {
        iconMap = new Dictionary<DieType, ElementPoolIcon>
        {
            { DieType.Shadow, shadowIcon },
            { DieType.Defense, defenseIcon },
            { DieType.Fire, fireIcon },
            { DieType.Ice, iceIcon },
            { DieType.Nature, natureIcon },
        };

        foreach (var kvp in iconMap)
        {
            if (kvp.Value == null)
                Debug.LogError($"ElementPoolDisplay: {kvp.Key} icon is not assigned!");
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
                icon.SetValue(kvp.Value);
        }
    }
}
