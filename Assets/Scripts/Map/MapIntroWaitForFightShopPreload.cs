using UnityEngine;

/// <summary>
/// Keeps assigned roots inactive until <see cref="RunManager"/> finishes additive fight + shop loads for a map run
/// (via <see cref="RunManager.OnMapFightShopPreloadUnhideVisuals"/>, before map bootstrap). When subscene preload is off or not a map run, does nothing.
/// </summary>
public sealed class MapIntroWaitForFightShopPreload : MonoBehaviour
{
    [Tooltip("Disabled on Awake (map run + preload on), then enabled when fight/shop additive preload finishes. Assign your intro Animator root(s) here.")]
    [SerializeField] private GameObject[] introRootsToEnableAfterPreload;

    private bool _activated;
    private RunManager _subscribedRunManager;

    bool ShouldDeferIntro()
    {
        var rm = RunManager.Instance;
        return rm != null && rm.UseMapBasedRun && rm.PreloadsFightShopOnMap;
    }

    void Awake()
    {
        if (introRootsToEnableAfterPreload == null || introRootsToEnableAfterPreload.Length == 0)
        {
            Debug.LogError($"{nameof(MapIntroWaitForFightShopPreload)} on '{name}': assign {nameof(introRootsToEnableAfterPreload)} with at least one non-null root.", this);
            return;
        }

        if (!ShouldDeferIntro())
            return;

        var anyNonNull = false;
        for (var i = 0; i < introRootsToEnableAfterPreload.Length; i++)
        {
            var root = introRootsToEnableAfterPreload[i];
            if (root == null)
                continue;
            anyNonNull = true;
            root.SetActive(false);
        }

        if (!anyNonNull)
            Debug.LogError($"{nameof(MapIntroWaitForFightShopPreload)} on '{name}': {nameof(introRootsToEnableAfterPreload)} has no non-null entries while map subscene preload is enabled.", this);
    }

    void Start()
    {
        if (introRootsToEnableAfterPreload == null || introRootsToEnableAfterPreload.Length == 0)
            return;

        if (!ShouldDeferIntro())
            return;

        var rm = RunManager.Instance;
        if (rm == null)
        {
            Debug.LogError($"{nameof(MapIntroWaitForFightShopPreload)} on '{name}': {nameof(RunManager)} instance is null — cannot wait for preload.", this);
            ActivateIntroRoots();
            return;
        }

        if (rm.IsMapFightShopPreloadFinishedForIntro())
        {
            ActivateIntroRoots();
            return;
        }

        rm.OnMapFightShopPreloadUnhideVisuals += OnUnhideVisualsAfterPreload;
        _subscribedRunManager = rm;
    }

    void OnDestroy()
    {
        if (_subscribedRunManager != null)
        {
            _subscribedRunManager.OnMapFightShopPreloadUnhideVisuals -= OnUnhideVisualsAfterPreload;
            _subscribedRunManager = null;
        }
    }

    void OnUnhideVisualsAfterPreload()
    {
        if (_subscribedRunManager != null)
        {
            _subscribedRunManager.OnMapFightShopPreloadUnhideVisuals -= OnUnhideVisualsAfterPreload;
            _subscribedRunManager = null;
        }

        ActivateIntroRoots();
    }

    void ActivateIntroRoots()
    {
        if (_activated)
            return;
        _activated = true;

        if (introRootsToEnableAfterPreload == null)
            return;

        for (var i = 0; i < introRootsToEnableAfterPreload.Length; i++)
        {
            var root = introRootsToEnableAfterPreload[i];
            if (root != null)
                root.SetActive(true);
        }
    }
}
