using System;
using UnityEngine;

/// <summary>
/// Spawns a rank's <see cref="PlayerRankSO.LargePortraitPrefab"/> under a slot transform.
/// Used by dice-select and rank-up popup portrait slots.
/// </summary>
public sealed class RankPortraitPrefabHost : MonoBehaviour
{
    const string RankPortraitWorldLayerName = "Rank Up Portrait World Layer";

    [SerializeField] private Transform spawnRoot;
    [SerializeField] private SplatterRevealSpritePlayer revealPlayer;
    [Tooltip("Order in Layer applied to every SpriteRenderer on the spawned rank portrait.")]
    [SerializeField] private int spawnedSortingOrder;
    [Tooltip("Optional sorting layer for spawned portrait sprites. Leave empty to keep each renderer's layer.")]
    [SerializeField] private string spawnedSortingLayerName;
    [Header("Canvas spawn root fix")]
    [Tooltip("When Spawn Root lives under a Canvas, reparent it to world space so SpriteRenderer portraits render at full scale.")]
    [SerializeField] private bool reparentCanvasSpawnRootToWorld = true;
    [SerializeField] private Vector3 worldPortraitLocalPosition = new(-0.39f, -4.85f, 0f);

    GameObject _spawnedInstance;
    Animator _spawnedAnimator;
    bool _spawnRootWorldSpaceReady;

    public GameObject SpawnedInstance => _spawnedInstance;
    public Animator SpawnedAnimator => _spawnedAnimator;

    /// <summary>Sorting applied the next time <see cref="ApplyRank"/> spawns a portrait.</summary>
    public void ConfigureSpawnedSorting(int sortingOrder, string sortingLayerName = null)
    {
        spawnedSortingOrder = sortingOrder;
        if (sortingLayerName != null)
            spawnedSortingLayerName = sortingLayerName;
    }

    public bool ApplyRank(PlayerRankSO rank)
    {
        Clear();

        if (rank == null)
            return false;

        var prefab = rank.LargePortraitPrefab;
        if (prefab == null)
        {
            Debug.LogError(
                $"{nameof(RankPortraitPrefabHost)} on '{name}': rank '{rank.name}' has no {nameof(PlayerRankSO.LargePortraitPrefab)} assigned.",
                this);
            return false;
        }

        ValidateSetup();
        EnsureSpawnRootWorldSpaceReady();

        if (!TryInstantiatePortraitPrefab(prefab, spawnRoot, out _spawnedInstance))
        {
            Debug.LogError(
                $"{nameof(RankPortraitPrefabHost)} on '{name}': could not instantiate large portrait prefab from rank '{rank.name}'. " +
                "Assign a display prefab (e.g. 'Arthur Lvl 1 Display Prefab') — do not assign a raw PSB or sprite sub-asset.",
                this);
            return false;
        }

        _spawnedInstance.name = prefab.name;
        _spawnedInstance.SetActive(false);
        SuppressPrefabAutoReveal(_spawnedInstance);

        if (!TryResolvePrefabAnimator(out _spawnedAnimator))
        {
            Debug.LogError(
                $"{nameof(RankPortraitPrefabHost)} on '{name}': spawned prefab '{prefab.name}' has no {nameof(Animator)}. " +
                "Add an Animator + per-rig Animator Controller on the PSB prefab.",
                this);
        }
        else
        {
            _spawnedAnimator.Rebind();
            _spawnedAnimator.Update(0f);
        }

        ApplySpawnedSortingOrder();
        _spawnedInstance.SetActive(true);
        return true;
    }

    public void RebindAnimator()
    {
        if (_spawnedAnimator == null)
            return;

        _spawnedAnimator.Rebind();
        _spawnedAnimator.Update(0f);
    }

    /// <summary>Prepares splatter materials on spawned sprite renderers for manual reveal drive.</summary>
    public bool PrepareManualReveal(Material materialSource)
    {
        var player = ResolveRevealPlayer();
        if (player == null)
        {
            Debug.LogError($"{nameof(RankPortraitPrefabHost)} on '{name}': no {nameof(SplatterRevealSpritePlayer)} assigned.", this);
            return false;
        }

        var scanRoot = _spawnedInstance != null ? _spawnedInstance.transform : null;
        return player.PrepareManualTargets(materialSource, scanRoot);
    }

    public void SetManualRevealImmediate(float reveal01)
    {
        var player = ResolveRevealPlayer();
        if (player != null)
            player.SetManualRevealImmediate(reveal01);
    }

    public void Clear()
    {
        ResolveRevealPlayer()?.ClearTargets();

        if (_spawnedInstance == null)
            return;

        if (Application.isPlaying)
            Destroy(_spawnedInstance);
        else
            DestroyImmediate(_spawnedInstance);

        _spawnedInstance = null;
        _spawnedAnimator = null;
    }

    void OnDestroy() => Clear();

    void ValidateSetup()
    {
        if (spawnRoot == null)
            throw new InvalidOperationException(
                $"{nameof(RankPortraitPrefabHost)} on '{name}': assign {nameof(spawnRoot)}.");
    }

    void EnsureSpawnRootWorldSpaceReady()
    {
        if (_spawnRootWorldSpaceReady || spawnRoot == null || !reparentCanvasSpawnRootToWorld)
            return;

        if (spawnRoot.GetComponentInParent<Canvas>(true) == null)
        {
            _spawnRootWorldSpaceReady = true;
            return;
        }

        var worldLayer = ResolveRankPortraitWorldLayer();
        spawnRoot.SetParent(worldLayer, worldPositionStays: false);
        spawnRoot.localPosition = worldPortraitLocalPosition;
        spawnRoot.localRotation = Quaternion.identity;
        spawnRoot.localScale = Vector3.one;
        _spawnRootWorldSpaceReady = true;
    }

    static Transform ResolveRankPortraitWorldLayer()
    {
        var existing = GameObject.Find(RankPortraitWorldLayerName);
        if (existing != null)
            return existing.transform;

        return new GameObject(RankPortraitWorldLayerName).transform;
    }

    SplatterRevealSpritePlayer ResolveRevealPlayer()
    {
        if (revealPlayer != null)
            return revealPlayer;

        revealPlayer = GetComponent<SplatterRevealSpritePlayer>();
        return revealPlayer;
    }

    bool TryResolvePrefabAnimator(out Animator animator)
    {
        animator = null;
        if (_spawnedInstance == null)
            return false;

        animator = _spawnedInstance.GetComponent<Animator>();
        if (animator == null)
            animator = _spawnedInstance.GetComponentInChildren<Animator>(true);

        return animator != null;
    }

    void ApplySpawnedSortingOrder()
    {
        if (_spawnedInstance == null)
            return;

        var renderers = _spawnedInstance.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingOrder = spawnedSortingOrder;
            if (!string.IsNullOrWhiteSpace(spawnedSortingLayerName))
                renderers[i].sortingLayerName = spawnedSortingLayerName;
        }
    }

    static void SuppressPrefabAutoReveal(GameObject spawnedInstance)
    {
        var players = spawnedInstance.GetComponentsInChildren<SplatterRevealSpritePlayer>(true);
        for (var i = 0; i < players.Length; i++)
            players[i].enabled = false;
    }

    static bool TryInstantiatePortraitPrefab(GameObject prefab, Transform parent, out GameObject instance)
    {
        instance = null;
        if (prefab == null || parent == null)
            return false;

        // Avoid Instantiate<GameObject> — raw PSB root references can clone as a non-GameObject Object.
        var clone = UnityEngine.Object.Instantiate((UnityEngine.Object)prefab, parent, worldPositionStays: false);
        instance = clone as GameObject;
        if (instance != null)
            return true;

        if (clone != null)
            UnityEngine.Object.Destroy(clone);

        return false;
    }
}
