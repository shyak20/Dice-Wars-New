using System;
using UnityEngine;

/// <summary>
/// Dice-select large portrait host: instantiates the active rank's PSB prefab under a spawn slot.
/// Animation comes from the prefab's own <see cref="Animator"/> (clip recorded on that rig).
/// Do not assign a shared controller here — absolute bone curves from one character break others.
/// </summary>
public sealed class DiceSelectLargePortraitPresenter : MonoBehaviour
{
    [SerializeField] private Transform spawnRoot;

    [Header("Portrait reveal fallback")]
    [Tooltip("Used when the spawned prefab has no SplatterRevealSpritePlayer (e.g. raw PSB). Display prefabs should include that component.")]
    [SerializeField] private Material portraitRevealMaterialFallback;

    GameObject _spawnedInstance;
    Animator _spawnedAnimator;

    public void ApplyCharacter(PlayerDataSO character, bool replayAnimatorReveal = false)
    {
        var rank = ProgressionRankPortraitUtility.GetActiveRank(character);
        ApplyRank(rank, replayAnimatorReveal);
    }

    public void ApplyRank(PlayerRankSO rank, bool replayAnimatorReveal = false)
    {
        ValidateSetup();

        ClearSpawnedInstance();

        if (rank == null)
        {
            SetSpawnRootActive(false);
            return;
        }

        var prefab = rank.LargePortraitPrefab;
        if (prefab == null)
        {
            Debug.LogError(
                $"{nameof(DiceSelectLargePortraitPresenter)} on '{name}': rank '{rank.name}' has no {nameof(PlayerRankSO.LargePortraitPrefab)} assigned.",
                this);
            SetSpawnRootActive(false);
            return;
        }

        SetSpawnRootActive(true);
        _spawnedInstance = Instantiate(prefab, spawnRoot, worldPositionStays: false);
        _spawnedInstance.name = prefab.name;

        if (!TryResolvePrefabAnimator(out _spawnedAnimator))
        {
            Debug.LogError(
                $"{nameof(DiceSelectLargePortraitPresenter)} on '{name}': spawned prefab '{prefab.name}' has no {nameof(Animator)}. " +
                "Add an Animator + per-rig Animator Controller on the PSB prefab (record the clip on that rig, not another character's).",
                this);
        }
        else
        {
            _spawnedAnimator.Rebind();
            _spawnedAnimator.Update(0f);
        }

        ReplayAnimatorIfRequested(replayAnimatorReveal);
        EnsurePortraitReveal(replayAnimatorReveal);
    }

    void ValidateSetup()
    {
        if (spawnRoot == null)
            throw new InvalidOperationException(
                $"{nameof(DiceSelectLargePortraitPresenter)} on '{name}': assign {nameof(spawnRoot)}.");
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

    void ReplayAnimatorIfRequested(bool replayAnimatorReveal)
    {
        if (!replayAnimatorReveal || _spawnedAnimator == null)
            return;

        _spawnedAnimator.Rebind();
        _spawnedAnimator.Update(0f);
    }

    void EnsurePortraitReveal(bool replayReveal)
    {
        if (_spawnedInstance == null)
            return;

        var reveal = _spawnedInstance.GetComponentInChildren<SplatterRevealSpritePlayer>(true);
        if (reveal != null)
        {
            if (replayReveal)
                reveal.PlayReveal();
            return;
        }

        if (portraitRevealMaterialFallback == null)
            return;

        reveal = _spawnedInstance.AddComponent<SplatterRevealSpritePlayer>();
        reveal.PlayReveal(portraitRevealMaterialFallback);
    }

    void SetSpawnRootActive(bool active)
    {
        if (spawnRoot != null)
            spawnRoot.gameObject.SetActive(active);
    }

    void ClearSpawnedInstance()
    {
        if (_spawnedInstance == null)
            return;

        if (Application.isPlaying)
            Destroy(_spawnedInstance);
        else
            DestroyImmediate(_spawnedInstance);

        _spawnedInstance = null;
        _spawnedAnimator = null;
    }

    void OnDestroy() => ClearSpawnedInstance();
}
