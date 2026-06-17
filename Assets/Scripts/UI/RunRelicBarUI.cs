using UnityEngine;

/// <summary>Displays icons for relics on the current run (map, shop, combat). Wire a horizontal layout and slot prefab (root with <see cref="RunRelicSlotView"/>).</summary>
public sealed class RunRelicBarUI : MonoBehaviour
{
    [Tooltip("Root object to hide when the player has no relics. Defaults to iconParent object when not assigned.")]
    [SerializeField] private GameObject relicsParent;
    [SerializeField] private Transform iconParent;
    [Tooltip("Prefab root must include RunRelicSlotView.")]
    [SerializeField] private GameObject slotPrefab;

    /// <summary>
    /// Prevents re-entrancy: enabling the bar (<see cref="GameObject.SetActive"/>) from inside <see cref="Rebuild"/>
    /// synchronously runs <see cref="OnEnable"/>, which would otherwise call <see cref="Rebuild"/> again and spawn duplicate slots.
    /// </summary>
    bool _inRebuild;

    private void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            // Idempotent: OnEnable can run again after disable without stacking handlers.
            RunManager.Instance.OnRunRelicsChanged -= Rebuild;
            RunManager.Instance.OnRunRelicsChanged += Rebuild;
        }

        if (!_inRebuild)
            Rebuild();
    }

    private void OnDisable()
    {
        // Do not unsubscribe here: map-based runs deactivate the whole map while the shop loads
        // additively — we still need relic updates so the bar is correct when returning to the map.
    }

    private void OnDestroy()
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

        if (_inRebuild)
            return;

        _inRebuild = true;
        try
        {
            for (var i = iconParent.childCount - 1; i >= 0; i--)
            {
                var child = iconParent.GetChild(i);
                if (child != null && child.GetComponent<RunRelicSlotView>() != null)
                    Destroy(child.gameObject);
            }

            if (slotPrefab == null || RunManager.Instance == null)
                return;

            var relicCount = RunManager.Instance.RunRelics != null ? RunManager.Instance.RunRelics.Count : 0;
            var parentToToggle = relicsParent != null ? relicsParent : iconParent.gameObject;

            if (relicCount == 0)
            {
                if (parentToToggle != null)
                    parentToToggle.SetActive(false);
                return;
            }

            foreach (var r in RunManager.Instance.RunRelics)
            {
                if (r == null)
                    continue;
                var instance = Instantiate(slotPrefab, iconParent);
                var view = instance.GetComponent<RunRelicSlotView>();
                if (view == null)
                {
                    Debug.LogError("RunRelicBarUI: slot prefab needs RunRelicSlotView on the root.", slotPrefab);
                    Destroy(instance);
                    continue;
                }

                view.Bind(r);
            }

            if (parentToToggle != null)
                parentToToggle.SetActive(true);
        }
        finally
        {
            _inRebuild = false;
        }
    }
}
