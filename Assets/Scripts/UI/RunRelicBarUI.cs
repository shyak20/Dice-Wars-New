using System.Collections.Generic;
using UnityEngine;

/// <summary>Displays icons for relics on the current run (map, shop, combat). Wire a horizontal layout and slot prefab (root with <see cref="RunRelicSlotView"/>).</summary>
public sealed class RunRelicBarUI : MonoBehaviour
{
    [SerializeField] private Transform iconParent;
    [Tooltip("Prefab root must include RunRelicSlotView.")]
    [SerializeField] private GameObject slotPrefab;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private void OnEnable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunRelicsChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunRelicsChanged -= Rebuild;
    }

    private void Rebuild()
    {
        if (iconParent == null)
        {
            Debug.LogError("RunRelicBarUI: assign iconParent.", this);
            return;
        }

        foreach (var go in _spawned)
        {
            if (go != null)
                Destroy(go);
        }

        _spawned.Clear();

        if (slotPrefab == null || RunManager.Instance == null)
            return;

        foreach (var r in RunManager.Instance.RunRelics)
        {
            if (r == null) continue;
            var instance = Instantiate(slotPrefab, iconParent);
            var view = instance.GetComponent<RunRelicSlotView>();
            if (view == null)
            {
                Debug.LogError("RunRelicBarUI: slot prefab needs RunRelicSlotView on the root.", slotPrefab);
                Destroy(instance);
                continue;
            }

            view.Bind(r);
            _spawned.Add(instance);
        }
    }
}
