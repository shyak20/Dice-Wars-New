using UnityEngine;

/// <summary>
/// Dice-select large portrait host: instantiates the active rank's PSB prefab under a spawn slot.
/// Animation comes from the prefab's own <see cref="Animator"/> (clip recorded on that rig).
/// Do not assign a shared controller here — absolute bone curves from one character break others.
/// </summary>
[RequireComponent(typeof(RankPortraitPrefabHost))]
public sealed class DiceSelectLargePortraitPresenter : MonoBehaviour
{
    [Header("Portrait reveal fallback")]
    [Tooltip("Used when the spawned prefab has no SplatterRevealSpritePlayer (e.g. raw PSB). Display prefabs should include that component.")]
    [SerializeField] private Material portraitRevealMaterialFallback;

    RankPortraitPrefabHost _host;
    PlayerRankSO _spawnedRank;

    void Awake() => _host = GetComponent<RankPortraitPrefabHost>();

    public void ApplyCharacter(PlayerDataSO character, bool replayAnimatorReveal = false)
    {
        var rank = ProgressionRankPortraitUtility.GetActiveRank(character);
        ApplyRank(rank, replayAnimatorReveal);
    }

    public void ApplyRank(PlayerRankSO rank, bool replayAnimatorReveal = false)
    {
        if (rank == null)
        {
            _spawnedRank = null;
            _host.Clear();
            SetSpawnRootActive(false);
            return;
        }

        if (!replayAnimatorReveal && _spawnedRank == rank && _host.SpawnedInstance != null)
        {
            SetSpawnRootActive(true);
            return;
        }

        _spawnedRank = rank;
        _host.Clear();

        if (!_host.ApplyRank(rank))
        {
            _spawnedRank = null;
            SetSpawnRootActive(false);
            return;
        }

        SetSpawnRootActive(true);

        if (replayAnimatorReveal)
            _host.RebindAnimator();

        EnsurePortraitReveal(replayAnimatorReveal);
    }

    public void SetSpawnRootActive(bool active)
    {
        if (_host != null && _host.transform != null)
            _host.gameObject.SetActive(active);
    }

    void EnsurePortraitReveal(bool replayReveal)
    {
        var spawnedInstance = _host.SpawnedInstance;
        if (spawnedInstance == null)
            return;

        var reveal = spawnedInstance.GetComponentInChildren<SplatterRevealSpritePlayer>(true);
        if (reveal != null)
        {
            reveal.enabled = true;
            reveal.PlayReveal();
            return;
        }

        if (portraitRevealMaterialFallback == null)
            return;

        reveal = spawnedInstance.AddComponent<SplatterRevealSpritePlayer>();
        reveal.PlayReveal(portraitRevealMaterialFallback);
    }

    void OnDestroy() => _host?.Clear();
}
