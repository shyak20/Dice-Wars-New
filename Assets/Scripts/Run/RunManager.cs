using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [SerializeField] private EncounterListSO encounterList;
    [SerializeField] private string combatSceneName = "FightScene";
    [SerializeField] private string shopSceneName = "ShopScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Map-based run")]
    [Tooltip("When true, StartRun() loads the map scene instead of the encounter list.")]
    [SerializeField] private bool useMapInsteadOfEncounterList;
    [Tooltip("When map run is enabled, load this scene first so the player picks starting dice; then Continue loads the map.")]
    [SerializeField] private bool loadDiceSelectBeforeMap = true;
    [SerializeField] private string diceSelectSceneName = "DiceSelect";
    [SerializeField] private string mapSceneName = "MapScene";
    [SerializeField] private MapActDefinitionSO[] mapActDefinitionsByAct = new MapActDefinitionSO[3];
    [SerializeField, Min(0)] private int mapWeightNormal = 4;
    [SerializeField, Min(0)] private int mapWeightShop = 2;
    [SerializeField, Min(0)] private int mapWeightShrine = 2;
    [SerializeField, Min(0)] private int mapWeightUnknown = 1;
    [SerializeField, Min(0)] private int mapWeightTreasure = 1;

    [Header("UI")]
    [Tooltip("Register once for shop/reward/menu scenes; combat can also assign on CombatManager.")]
    [SerializeField] private GameIconIndexSO gameIconIndex;

    private int currentRoomIndex;
    private bool _useMapBasedRun;
    private int _currentActIndex;
    private int _runShrineBonusMaxPower;
    private bool _runVitalityInitialized;
    private int _runCurrentHp;
    private int _runMaxHp;
    private int _runPermanentStrengthStacksFromSpecialEvents;

    [Header("Unknown map — permanent Strength")]
    [Tooltip("Each combat start applies this many stacks per accumulated map bonus (Fossilized D6, etc.). Assign Strength status asset.")]
    [SerializeField] private StrengthEffectSO mapRunPermanentStrengthDefinition;

    private MapGrid _persistedMapGrid;
    private Vector2Int _persistedPlayerCell;
    private int _persistedMovesTaken;
    private bool _hasPersistedMapState;

    private readonly System.Random _enemyDrawRng = new System.Random();
    private readonly Dictionary<EnemyRank, HashSet<EnemyTypeSO>> _drawnUniquesThisMap = new Dictionary<EnemyRank, HashSet<EnemyTypeSO>>();
    private readonly HashSet<UnknownMapEventSO> _drawnUnknownThisMap = new HashSet<UnknownMapEventSO>();
    private readonly HashSet<string> _drawnUnknownEventIdsThisMap = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _completedUnknownMapEventIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<EnemyTypeSO> _enemyDrawScratch = new List<EnemyTypeSO>();
    private readonly List<UnknownMapEventSO> _unknownValidScratch = new List<UnknownMapEventSO>();
    private readonly List<UnknownMapEventSO> _unknownUnusedScratch = new List<UnknownMapEventSO>();
    private readonly List<RelicSO> _runRelics = new List<RelicSO>();

    [Header("Map subscene preload (additive)")]
    [Tooltip("When the map scene loads, Fight and Shop are loaded additively with roots hidden so the first transition avoids a cold load. On show, each root’s active state is restored to how it was right after load (matches scene defaults).")]
    [SerializeField] private bool preloadFightAndShopWhileOnMap = true;

    private Coroutine _preloadMapSubscenesRoutine;
    private bool _fightScenePreloadedForMapRun;
    private bool _shopScenePreloadedForMapRun;
    private readonly List<SceneRootActiveSnapshot> _fightRootDefaultActives = new List<SceneRootActiveSnapshot>();
    private readonly List<SceneRootActiveSnapshot> _shopRootDefaultActives = new List<SceneRootActiveSnapshot>();
    /// <summary>Map root actives stashed when opening fight/shop so the map scene stays loaded but hidden (mirrors subscene hiding on the map).</summary>
    private readonly List<SceneRootActiveSnapshot> _mapRootStashedWhenLeavingForSubScene = new List<SceneRootActiveSnapshot>();

    private struct SceneRootActiveSnapshot
    {
        public GameObject Root;
        public bool DefaultActiveSelf;
    }

    public bool UseMapBasedRun => _useMapBasedRun;
    public bool PreloadsFightShopOnMap => preloadFightAndShopWhileOnMap;
    public int CurrentActIndex => _currentActIndex;
    public int RunShrineBonusMaxPower => _runShrineBonusMaxPower;

    /// <summary>Current run HP (map + combat). Call <see cref="EnsureRunVitalityBaseline"/> via getters before first use.</summary>
    public int RunCurrentHp
    {
        get
        {
            EnsureRunVitalityBaseline();
            return _runCurrentHp;
        }
    }

    public int RunMaxHp
    {
        get
        {
            EnsureRunVitalityBaseline();
            return _runMaxHp;
        }
    }

    /// <summary>Fired after run HP changes from map overflow, shrine heal, etc. (not every combat frame).</summary>
    public event Action OnRunVitalityChanged;

    /// <summary>Fired when shrine max-power bonus changes (map HUD max-power meter).</summary>
    public event Action OnRunMaxPowerBudgetChanged;

    /// <summary>Relics collected this run (shop, treasure, etc.).</summary>
    public IReadOnlyList<RelicSO> RunRelics => _runRelics;

    public event Action OnRunRelicsChanged;

    /// <summary>Fired first when fight + shop preload completes — enable map visuals (e.g. intro roots) so UI under them is active before bootstrap.</summary>
    public event Action OnMapFightShopPreloadUnhideVisuals;

    /// <summary>Fired after <see cref="OnMapFightShopPreloadUnhideVisuals"/> when additive fight + shop loads have finished for the map run.</summary>
    public event Action OnMapFightShopPreloadFinished;

    public RoomDefinition CurrentRoom
    {
        get
        {
            if (encounterList == null || currentRoomIndex >= encounterList.rooms.Count)
            {
                Debug.LogError("RunManager: No valid room at index " + currentRoomIndex);
                return null;
            }
            return encounterList.rooms[currentRoomIndex];
        }
    }

    public bool HasNextRoom => encounterList != null && currentRoomIndex < encounterList.rooms.Count - 1;

    public bool IsMapFightShopPreloadFinishedForIntro()
    {
        if (!_useMapBasedRun)
            return true;
        if (!preloadFightAndShopWhileOnMap)
            return true;
        return _fightScenePreloadedForMapRun && _shopScenePreloadedForMapRun;
    }

    private void RaiseMapFightShopPreloadFinished()
    {
        OnMapFightShopPreloadUnhideVisuals?.Invoke();
        OnMapFightShopPreloadFinished?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (encounterList == null && !useMapInsteadOfEncounterList)
            Debug.LogError("RunManager: encounterList is not assigned (required when not using map run).");

        if (gameIconIndex != null)
            GameIconCatalog.Register(gameIconIndex);

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (Instance == this)
            Instance = null;
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (!next.IsValid())
            return;

        var nextName = next.name;
        if (!string.IsNullOrEmpty(mapSceneName) && nextName == mapSceneName)
        {
            ResetGlobalTimeScaleToOne();
            return;
        }

        if (!string.IsNullOrEmpty(combatSceneName) && nextName == combatSceneName)
            ApplyFightSceneSimulationSpeedFromLoadedScene();
    }

    private static void ResetGlobalTimeScaleToOne()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private void ApplyFightSceneSimulationSpeedFromLoadedScene()
    {
        if (string.IsNullOrEmpty(combatSceneName))
            return;

        var fightScene = SceneManager.GetSceneByName(combatSceneName);
        if (!fightScene.IsValid())
            return;

        var controllers = FindObjectsOfType<SimulationSpeedController>(true);
        for (var i = 0; i < controllers.Length; i++)
        {
            var c = controllers[i];
            if (c != null && c.gameObject.scene == fightScene)
            {
                c.ApplyConfiguredDefaultSpeed();
                return;
            }
        }

        ResetGlobalTimeScaleToOne();
    }

    public void StartRun()
    {
        if (RunEconomyManager.Instance != null)
            RunEconomyManager.Instance.ResetEconomyForNewRun();

        ClearRunRelics();
        _runVitalityInitialized = false;
        _completedUnknownMapEventIds.Clear();
        _runPermanentStrengthStacksFromSpecialEvents = 0;

        if (useMapInsteadOfEncounterList)
        {
            StartMapBasedRunInternal();
            return;
        }

        if (encounterList == null || encounterList.rooms.Count == 0)
        {
            Debug.LogError("RunManager: Cannot start run — encounter list is empty or null!");
            return;
        }

        _useMapBasedRun = false;
        currentRoomIndex = 0;
        LoadCurrentRoom();
    }

    private void StartMapBasedRunInternal()
    {
        if (mapActDefinitionsByAct == null || mapActDefinitionsByAct.Length < 3)
        {
            Debug.LogError("RunManager: mapActDefinitionsByAct must have 3 entries (one per act).");
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            if (mapActDefinitionsByAct[i] == null)
            {
                Debug.LogError($"RunManager: mapActDefinitionsByAct[{i}] is not assigned.");
                return;
            }
        }

        if (string.IsNullOrEmpty(mapSceneName))
        {
            Debug.LogError("RunManager: mapSceneName is not assigned.");
            return;
        }

        _useMapBasedRun = true;
        _currentActIndex = 0;
        _runShrineBonusMaxPower = 0;
        _runPermanentStrengthStacksFromSpecialEvents = 0;
        _runVitalityInitialized = false;
        ClearMapPersistenceForNewAct();
        ClearMapEncounterDrawState();
        currentRoomIndex = 0;

        if (loadDiceSelectBeforeMap)
        {
            if (string.IsNullOrEmpty(diceSelectSceneName))
            {
                Debug.LogError("RunManager: loadDiceSelectBeforeMap is true but diceSelectSceneName is empty.");
                return;
            }

            SceneManager.LoadScene(diceSelectSceneName);
            return;
        }

        ClearMapSubsceneTransitionSnapshot();
        SceneManager.LoadScene(mapSceneName);
    }

    /// <summary>Called from <see cref="DiceSelectSceneController"/> after starting dice are written to <see cref="PlayerDataContainer"/>.</summary>
    public void LoadMapAfterDiceSelect()
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.LoadMapAfterDiceSelect: not in a map-based run.");
            return;
        }

        if (string.IsNullOrEmpty(mapSceneName))
        {
            Debug.LogError("RunManager.LoadMapAfterDiceSelect: mapSceneName is not assigned.");
            return;
        }

        ClearMapSubsceneTransitionSnapshot();
        SceneManager.LoadScene(mapSceneName);
    }

    public MapGenerationEventsParams GetMapGenerationParamsForCurrentAct()
    {
        var act = GetActDefinitionOrThrow();
        return new MapGenerationEventsParams
        {
            EliteMinCount = act.eliteMinOnMap,
            EliteMaxCount = act.eliteMaxOnMap,
            ShopMinCount = act.shopMinOnMap,
            ShopMaxCount = act.shopMaxOnMap,
            ShrineMinCount = act.shrineMinOnMap,
            ShrineMaxCount = act.shrineMaxOnMap,
            UnknownMinCount = act.unknownMinOnMap,
            UnknownMaxCount = act.unknownMaxOnMap,
            TreasureMinCount = act.treasureMinOnMap,
            TreasureMaxCount = act.treasureMaxOnMap,
            MinStepsElite = act.eliteMinStepsFromStart,
            MinStepsShop = act.shopMinStepsFromStart,
            MinStepsShrine = act.shrineMinStepsFromStart,
            MinStepsUnknown = act.unknownMinStepsFromStart,
            MinStepsTreasure = act.treasureMinStepsFromStart,
            WeightNormal = mapWeightNormal,
            WeightShop = mapWeightShop,
            WeightShrine = mapWeightShrine,
            WeightUnknown = mapWeightUnknown,
            WeightTreasure = mapWeightTreasure
        };
    }

    public void AddRunRelic(RelicSO relic)
    {
        if (relic == null)
        {
            Debug.LogError("RunManager.AddRunRelic: relic is null.");
            return;
        }

        _runRelics.Add(relic);
        OnRunRelicsChanged?.Invoke();
    }

    public bool RemoveRunRelic(RelicSO relic)
    {
        if (relic == null)
            return false;
        var removed = _runRelics.Remove(relic);
        if (removed)
            OnRunRelicsChanged?.Invoke();
        return removed;
    }

    void ClearRunRelics()
    {
        _runRelics.Clear();
        OnRunRelicsChanged?.Invoke();
    }

    /// <summary>Random treasure pack for the current act’s map (null if none configured).</summary>
    public MapTreasurePackSO DrawRandomTreasurePack()
    {
        var act = GetCurrentMapActDefinitionOrNull();
        if (act?.treasurePacks == null || act.treasurePacks.Count == 0)
            return null;

        var valid = new List<MapTreasurePackSO>();
        foreach (var p in act.treasurePacks)
        {
            if (p != null)
                valid.Add(p);
        }

        if (valid.Count == 0)
            return null;
        return valid[_enemyDrawRng.Next(valid.Count)];
    }

    public void OnNewMapGeneratedCleanupDraws()
    {
        if (!_useMapBasedRun)
            return;
        _drawnUniquesThisMap.Clear();
        _drawnUnknownThisMap.Clear();
        _drawnUnknownEventIdsThisMap.Clear();
    }

    public void SaveMapPersistence(MapGrid gridClone, Vector2Int playerCell, int movesTaken)
    {
        _persistedMapGrid = gridClone ?? throw new ArgumentNullException(nameof(gridClone));
        _persistedPlayerCell = playerCell;
        _persistedMovesTaken = movesTaken;
        _hasPersistedMapState = true;
    }

    public bool TryRestorePersistedMap(out MapGrid grid, out Vector2Int playerCell, out int movesTaken)
    {
        if (!_hasPersistedMapState || _persistedMapGrid == null)
        {
            grid = null;
            playerCell = default;
            movesTaken = 0;
            return false;
        }

        grid = _persistedMapGrid;
        playerCell = _persistedPlayerCell;
        movesTaken = _persistedMovesTaken;
        _persistedMapGrid = null;
        _hasPersistedMapState = false;
        return true;
    }

    /// <summary>Clears any saved map return snapshot (new act or map regenerated in the editor / dev UI).</summary>
    public void ClearPersistedMapState()
    {
        _persistedMapGrid = null;
        _hasPersistedMapState = false;
    }

    public void ClearMapPersistenceForNewAct() => ClearPersistedMapState();

    public void PersistAndLoadFightScene(MapGrid grid, Vector2Int playerCell, int movesTaken, EnemyRank rank,
        bool isBossEndTile)
    {
        var enemy = DrawEnemyForMapCombat(rank);
        PersistAndLoadFightSceneWithEnemy(grid, playerCell, movesTaken, enemy, isBossEndTile);
    }

    /// <summary>Map combat with an explicit enemy (e.g. Unknown tile event with <see cref="UnknownMapEventSO.specificEnemy"/>).</summary>
    public void PersistAndLoadFightSceneWithEnemy(MapGrid grid, Vector2Int playerCell, int movesTaken,
        EnemyTypeSO enemy, bool isBossEndTile)
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.PersistAndLoadFightSceneWithEnemy: not in map-based run.");
            return;
        }

        if (enemy == null)
        {
            Debug.LogError("RunManager.PersistAndLoadFightSceneWithEnemy: enemy is null.");
            return;
        }

        SaveMapPersistence(grid, playerCell, movesTaken);
        RunEncounterBuffer.SetPendingCombat(enemy, isBossEndTile);
        if (TryEnterPreloadedCombatFromMap())
            return;
        SceneManager.LoadScene(combatSceneName);
    }

    public void PersistAndLoadShopScene(MapGrid grid, Vector2Int playerCell, int movesTaken)
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.PersistAndLoadShopScene: not in map-based run.");
            return;
        }

        if (string.IsNullOrEmpty(shopSceneName))
        {
            Debug.LogError("RunManager: shopSceneName is not assigned.");
            return;
        }

        SaveMapPersistence(grid, playerCell, movesTaken);
        if (TryEnterPreloadedShopFromMap())
            return;
        SceneManager.LoadScene(shopSceneName);
    }

    /// <summary>
    /// Called when the map scene has finished its own setup so Fight/Shop can be additively preloaded in the background.
    /// </summary>
    public void NotifyMapSceneReadyForSubscenePreload()
    {
        if (!_useMapBasedRun || !preloadFightAndShopWhileOnMap)
            return;
        if (IsMapFightShopPreloadFinishedForIntro())
            return;
        if (string.IsNullOrEmpty(combatSceneName) || string.IsNullOrEmpty(shopSceneName))
        {
            Debug.LogError("RunManager.NotifyMapSceneReadyForSubscenePreload: combatSceneName and shopSceneName must be set.");
            return;
        }

        if (_preloadMapSubscenesRoutine != null)
            return;

        _preloadMapSubscenesRoutine = StartCoroutine(CoPreloadFightAndShopAdditiveForMapRun());
    }

    private IEnumerator CoPreloadFightAndShopAdditiveForMapRun()
    {
        _fightScenePreloadedForMapRun = false;
        _shopScenePreloadedForMapRun = false;
        _fightRootDefaultActives.Clear();
        _shopRootDefaultActives.Clear();
        yield return null;

        if (!IsSceneLoadedByName(combatSceneName))
        {
            var op = SceneManager.LoadSceneAsync(combatSceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"RunManager: could not start additive load of '{combatSceneName}' — is it in Build Settings?", this);
                _preloadMapSubscenesRoutine = null;
                RaiseMapFightShopPreloadFinished();
                yield break;
            }

            while (!op.isDone)
                yield return null;

            var combatScene = SceneManager.GetSceneByName(combatSceneName);
            if (combatScene.IsValid() && combatScene.isLoaded)
                CaptureRootActivesThenHideScene(combatScene, _fightRootDefaultActives);
        }
        else
        {
            var combatScene = SceneManager.GetSceneByName(combatSceneName);
            if (combatScene.IsValid() && combatScene.isLoaded && _fightRootDefaultActives.Count == 0)
                CaptureRootActivesThenHideScene(combatScene, _fightRootDefaultActives);
        }

        _fightScenePreloadedForMapRun = IsSceneLoadedByName(combatSceneName) && _fightRootDefaultActives.Count > 0;

        if (!IsSceneLoadedByName(shopSceneName))
        {
            var opShop = SceneManager.LoadSceneAsync(shopSceneName, LoadSceneMode.Additive);
            if (opShop == null)
            {
                Debug.LogError($"RunManager: could not start additive load of '{shopSceneName}' — is it in Build Settings?", this);
                _preloadMapSubscenesRoutine = null;
                RaiseMapFightShopPreloadFinished();
                yield break;
            }

            while (!opShop.isDone)
                yield return null;

            var shopScene = SceneManager.GetSceneByName(shopSceneName);
            if (shopScene.IsValid() && shopScene.isLoaded)
                CaptureRootActivesThenHideScene(shopScene, _shopRootDefaultActives);
        }
        else
        {
            var shopScene = SceneManager.GetSceneByName(shopSceneName);
            if (shopScene.IsValid() && shopScene.isLoaded && _shopRootDefaultActives.Count == 0)
                CaptureRootActivesThenHideScene(shopScene, _shopRootDefaultActives);
        }

        _shopScenePreloadedForMapRun = IsSceneLoadedByName(shopSceneName) && _shopRootDefaultActives.Count > 0;
        _preloadMapSubscenesRoutine = null;
        RaiseMapFightShopPreloadFinished();
    }

    private bool TryEnterPreloadedCombatFromMap()
    {
        if (!_fightScenePreloadedForMapRun || _fightRootDefaultActives.Count == 0)
            return false;
        var combatScene = SceneManager.GetSceneByName(combatSceneName);
        if (!combatScene.IsValid() || !combatScene.isLoaded)
            return false;

        StashAndDeactivateMapSceneRoots();
        RestoreSceneRootsToCapturedDefaults(_fightRootDefaultActives);
        SceneManager.SetActiveScene(combatScene);
        return true;
    }

    private bool TryEnterPreloadedShopFromMap()
    {
        if (!_shopScenePreloadedForMapRun || _shopRootDefaultActives.Count == 0)
            return false;
        var shopScene = SceneManager.GetSceneByName(shopSceneName);
        if (!shopScene.IsValid() || !shopScene.isLoaded)
            return false;

        StashAndDeactivateMapSceneRoots();
        RestoreSceneRootsToCapturedDefaults(_shopRootDefaultActives);
        SceneManager.SetActiveScene(shopScene);
        return true;
    }

    private void ClearMapSubsceneTransitionSnapshot()
    {
        _mapRootStashedWhenLeavingForSubScene.Clear();
    }

    /// <summary>Records each map root’s <see cref="GameObject.activeSelf"/> then deactivates roots so fight/shop can become active without unloading the map.</summary>
    private void StashAndDeactivateMapSceneRoots()
    {
        _mapRootStashedWhenLeavingForSubScene.Clear();
        if (string.IsNullOrEmpty(mapSceneName))
            return;
        var mapScene = SceneManager.GetSceneByName(mapSceneName);
        if (!mapScene.IsValid() || !mapScene.isLoaded)
            return;
        var roots = mapScene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null)
                continue;
            _mapRootStashedWhenLeavingForSubScene.Add(new SceneRootActiveSnapshot
            {
                Root = root,
                DefaultActiveSelf = root.activeSelf
            });
            root.SetActive(false);
        }
    }

    private static void DeactivateCapturedRoots(List<SceneRootActiveSnapshot> captured)
    {
        for (var i = 0; i < captured.Count; i++)
        {
            var e = captured[i];
            if (e.Root != null)
                e.Root.SetActive(false);
        }
    }

    private static bool IsSceneLoadedByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;
        var s = SceneManager.GetSceneByName(sceneName);
        return s.IsValid() && s.isLoaded;
    }

    /// <summary>
    /// Right after additive load: record each root’s <see cref="GameObject.activeSelf"/> (matches scene file defaults before gameplay),
    /// then deactivate every root so the scene stays hidden on the map.
    /// </summary>
    private static void CaptureRootActivesThenHideScene(Scene scene, List<SceneRootActiveSnapshot> into)
    {
        if (!scene.IsValid())
            return;
        into.Clear();
        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null)
                continue;
            into.Add(new SceneRootActiveSnapshot { Root = root, DefaultActiveSelf = root.activeSelf });
            root.SetActive(false);
        }
    }

    private static void RestoreSceneRootsToCapturedDefaults(List<SceneRootActiveSnapshot> captured)
    {
        for (var i = 0; i < captured.Count; i++)
        {
            var e = captured[i];
            if (e.Root != null)
                e.Root.SetActive(e.DefaultActiveSelf);
        }
    }

    /// <summary>
    /// Clears additive preload bookkeeping after fight/shop scenes unload so the next preload captures fresh root actives
    /// (nested UI like the element pool is not restored by <see cref="RestoreSceneRootsToCapturedDefaults"/> alone).
    /// </summary>
    private void ClearFightShopAdditivePreloadSnapshots()
    {
        _fightRootDefaultActives.Clear();
        _shopRootDefaultActives.Clear();
        _fightScenePreloadedForMapRun = false;
        _shopScenePreloadedForMapRun = false;
    }

    private IEnumerator CoUnloadFightAndShopScenesIfLoaded()
    {
        var combatScene = SceneManager.GetSceneByName(combatSceneName);
        if (combatScene.IsValid() && combatScene.isLoaded)
        {
            var op = SceneManager.UnloadSceneAsync(combatScene);
            if (op != null)
            {
                while (!op.isDone)
                    yield return null;
            }
        }

        var shopScene = SceneManager.GetSceneByName(shopSceneName);
        if (shopScene.IsValid() && shopScene.isLoaded)
        {
            var opShop = SceneManager.UnloadSceneAsync(shopScene);
            if (opShop != null)
            {
                while (!opShop.isDone)
                    yield return null;
            }
        }
    }

    private IEnumerator CoWaitUntilMapFightShopPreloadFinishedOrTimeout()
    {
        if (!_useMapBasedRun || !preloadFightAndShopWhileOnMap)
            yield break;

        const int maxFrames = 6000;
        var frames = 0;
        while (!IsMapFightShopPreloadFinishedForIntro() && frames++ < maxFrames)
            yield return null;

        if (!IsMapFightShopPreloadFinishedForIntro())
            Debug.LogError(
                $"RunManager: timed out waiting for additive preload of '{combatSceneName}' / '{shopSceneName}' after returning to the map. The next tile fight may fall back to a single-scene load.",
                this);
    }

    public void ReturnToMapFromSubScene()
    {
        if (string.IsNullOrEmpty(mapSceneName))
        {
            Debug.LogError("RunManager: mapSceneName is not assigned.");
            return;
        }

        StartCoroutine(CoReturnToMapFromSubScene());
    }

    private IEnumerator CoReturnToMapFromSubScene()
    {
        var mapScene = SceneManager.GetSceneByName(mapSceneName);
        if (mapScene.IsValid() && mapScene.isLoaded && _mapRootStashedWhenLeavingForSubScene.Count > 0)
        {
            DeactivateCapturedRoots(_fightRootDefaultActives);
            DeactivateCapturedRoots(_shopRootDefaultActives);

            if (_useMapBasedRun && preloadFightAndShopWhileOnMap)
            {
                yield return CoUnloadFightAndShopScenesIfLoaded();
                ClearFightShopAdditivePreloadSnapshots();
                NotifyMapSceneReadyForSubscenePreload();
                yield return CoWaitUntilMapFightShopPreloadFinishedOrTimeout();
            }
            else
            {
                _fightScenePreloadedForMapRun = IsSceneLoadedByName(combatSceneName) && _fightRootDefaultActives.Count > 0;
                _shopScenePreloadedForMapRun = IsSceneLoadedByName(shopSceneName) && _shopRootDefaultActives.Count > 0;
            }

            RestoreSceneRootsToCapturedDefaults(_mapRootStashedWhenLeavingForSubScene);
            _mapRootStashedWhenLeavingForSubScene.Clear();
            SceneManager.SetActiveScene(mapScene);
            yield break;
        }

        var combatScene = SceneManager.GetSceneByName(combatSceneName);
        if (combatScene.IsValid() && combatScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(combatScene);

        var shopScene = SceneManager.GetSceneByName(shopSceneName);
        if (shopScene.IsValid() && shopScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(shopScene);

        _fightRootDefaultActives.Clear();
        _shopRootDefaultActives.Clear();
        _fightScenePreloadedForMapRun = false;
        _shopScenePreloadedForMapRun = false;
        ClearMapSubsceneTransitionSnapshot();

        SceneManager.LoadScene(mapSceneName);
    }

    public EnemyTypeSO DrawEnemyForMapCombat(EnemyRank rank)
    {
        var act = GetActDefinitionOrThrow();
        act.CollectEnemiesForRank(rank, _enemyDrawScratch);
        var valid = _enemyDrawScratch;

        if (valid.Count == 0)
            throw new InvalidOperationException(
                $"RunManager: act {_currentActIndex} has no enemies with rank {rank} in possibleEnemies.");

        if (!_drawnUniquesThisMap.TryGetValue(rank, out var used))
        {
            used = new HashSet<EnemyTypeSO>();
            _drawnUniquesThisMap[rank] = used;
        }

        var unused = new List<EnemyTypeSO>();
        foreach (var e in valid)
        {
            if (!used.Contains(e))
                unused.Add(e);
        }

        EnemyTypeSO pick;
        if (unused.Count > 0)
        {
            pick = unused[_enemyDrawRng.Next(unused.Count)];
            used.Add(pick);
        }
        else
        {
            pick = valid[_enemyDrawRng.Next(valid.Count)];
        }

        return pick;
    }

    public void ClearMapEncounterDrawState()
    {
        _drawnUniquesThisMap.Clear();
        _drawnUnknownThisMap.Clear();
        _drawnUnknownEventIdsThisMap.Clear();
    }

    /// <summary>True if an unknown map event id was registered completed this run (see <see cref="RegisterUnknownMapEventCompleted"/>).</summary>
    public bool IsUnknownMapEventCompleted(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return false;
        return _completedUnknownMapEventIds.Contains(eventId.Trim());
    }

    /// <summary>Marks an unknown event id as completed for this run (Clear Event chains, visibility conditions).</summary>
    public void RegisterUnknownMapEventCompleted(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            Debug.LogWarning("RunManager.RegisterUnknownMapEventCompleted: event id is empty — ignored.");
            return;
        }

        _completedUnknownMapEventIds.Add(eventId.Trim());
    }

    /// <summary>
    /// Picks an unknown event for the current act (unique entries preferred until the pool is exhausted).
    /// Uses a minimal evaluation context (no grid). Prefer <see cref="DrawUnknownMapEvent(MapGrid, Vector2Int, int)"/> from the map when conditions may depend on position later.
    /// Returns null if the act defines no unknown events.
    /// </summary>
    public UnknownMapEventSO DrawUnknownMapEvent() =>
        DrawUnknownMapEventInternal(new UnknownMapEventEvaluationContext(this, null, default, 0));

    /// <summary>
    /// Same as <see cref="DrawUnknownMapEvent"/> with full <see cref="UnknownMapEventEvaluationContext"/> for visibility / option rules that read map state.
    /// </summary>
    public UnknownMapEventSO DrawUnknownMapEvent(MapGrid grid, Vector2Int playerCell, int movesTaken) =>
        DrawUnknownMapEventInternal(new UnknownMapEventEvaluationContext(this, grid, playerCell, movesTaken));

    private UnknownMapEventSO DrawUnknownMapEventInternal(UnknownMapEventEvaluationContext evalCtx)
    {
        var act = GetActDefinitionOrThrow();
        _unknownValidScratch.Clear();
        if (act.possibleUnknownEvents != null)
        {
            foreach (var u in act.possibleUnknownEvents)
            {
                if (u == null)
                    continue;
                if (u.excludeFromDrawIfCompletedThisRun && IsUnknownMapEventCompleted(u.ResolvedEventId))
                    continue;
                if (!UnknownMapEventConditionEvaluator.AllPass(u.visibilityConditions, evalCtx))
                    continue;
                _unknownValidScratch.Add(u);
            }
        }

        if (_unknownValidScratch.Count == 0)
        {
            Debug.LogWarning("RunManager.DrawUnknownMapEvent: no unknown events pass act/visibility for this act.");
            return null;
        }

        _unknownUnusedScratch.Clear();
        foreach (var u in _unknownValidScratch)
        {
            if (_drawnUnknownThisMap.Contains(u))
                continue;
            if (_drawnUnknownEventIdsThisMap.Contains(u.ResolvedEventId))
                continue;
            _unknownUnusedScratch.Add(u);
        }

        UnknownMapEventSO pick;
        if (_unknownUnusedScratch.Count > 0)
        {
            pick = _unknownUnusedScratch[_enemyDrawRng.Next(_unknownUnusedScratch.Count)];
            _drawnUnknownThisMap.Add(pick);
            _drawnUnknownEventIdsThisMap.Add(pick.ResolvedEventId);
        }
        else
        {
            pick = _unknownValidScratch[_enemyDrawRng.Next(_unknownValidScratch.Count)];
        }

        return pick;
    }

    /// <summary>
    /// Map runs: sync <see cref="PlayerStatus"/> from persisted run HP. Always safe to call after <see cref="EnsureRunVitalityBaseline"/>.
    /// Non-map runs: no-op (combat uses <see cref="PlayerStatus.ApplyStartingHealthFromPlayerData"/> only).
    /// </summary>
    public void ApplyRunVitalityToPlayerIfAny(PlayerStatus player)
    {
        if (player == null || !_useMapBasedRun)
            return;
        EnsureRunVitalityBaseline();
        ReconcileRunVitalityIfAwakeDefaultPoisoned();
        player.ApplyRunVitality(_runCurrentHp, _runMaxHp);
    }

    /// <summary>
    /// If run vitality was captured before first combat init, <see cref="PlayerStatus"/> Awake defaults (max 1) could have been stored — repair using PlayerData baseline.
    /// </summary>
    private void ReconcileRunVitalityIfAwakeDefaultPoisoned()
    {
        var baseline = ReadStartingMaxHealthFromPlayerData();
        if (baseline <= 1 || _runMaxHp != 1)
            return;
        _runMaxHp = baseline;
        _runCurrentHp = Mathf.Clamp(_runCurrentHp, 0, _runMaxHp);
        if (_runCurrentHp < 1)
            _runCurrentHp = _runMaxHp;
    }

    public void CaptureRunVitalityFromPlayer(PlayerStatus player)
    {
        if (player == null)
            return;
        _runCurrentHp = player.GetCurrentHealth();
        _runMaxHp = player.maxHealth;
        _runVitalityInitialized = true;
        NotifyRunVitalityChanged();
    }

    public void ApplyShrineHeal(int amount)
    {
        if (amount <= 0)
            return;
        EnsureRunVitalityBaseline();
        _runCurrentHp = Mathf.Min(_runCurrentHp + amount, _runMaxHp);
        NotifyRunVitalityChanged();
    }

    /// <summary>Map runs: set current run HP to max.</summary>
    public void HealRunVitalityToFull()
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.HealRunVitalityToFull: not in map-based run.");
            return;
        }

        EnsureRunVitalityBaseline();
        _runCurrentHp = _runMaxHp;
        NotifyRunVitalityChanged();
    }

    /// <summary>Map runs: change current run HP by <paramref name="delta"/> (negative = damage). Game over at 0.</summary>
    public void ApplyRunCurrentHpDelta(int delta)
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.ApplyRunCurrentHpDelta: not in map-based run.");
            return;
        }

        if (delta == 0)
            return;

        EnsureRunVitalityBaseline();
        _runCurrentHp = Mathf.Clamp(_runCurrentHp + delta, 0, _runMaxHp);
        NotifyRunVitalityChanged();

        if (_runCurrentHp <= 0)
            EndRunFromPlayerDefeat();
    }

    /// <summary>
    /// Map runs: change max HP. Positive delta also heals current HP by that amount (capped at new max); negative delta only lowers max and clamps current.
    /// </summary>
    public void ApplyRunMaxHpDelta(int delta)
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.ApplyRunMaxHpDelta: not in map-based run.");
            return;
        }

        if (delta == 0)
            return;

        EnsureRunVitalityBaseline();
        _runMaxHp = Mathf.Max(1, _runMaxHp + delta);
        if (delta > 0)
            _runCurrentHp = Mathf.Min(_runCurrentHp + delta, _runMaxHp);
        else
            _runCurrentHp = Mathf.Clamp(_runCurrentHp, 0, _runMaxHp);
        NotifyRunVitalityChanged();
    }

    /// <summary>Map runs: same as <see cref="ApplyRunMaxHpDelta"/> for a positive amount (raise max and heal).</summary>
    public void ApplyRunMaxHpIncreaseAndHeal(int amount)
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.ApplyRunMaxHpIncreaseAndHeal: not in map-based run.");
            return;
        }

        if (amount <= 0)
            return;

        ApplyRunMaxHpDelta(amount);
    }

    /// <summary>
    /// Map move overflow (beyond <see cref="MapMovementManager.MoveLimit"/>): subtracts HP and shows floating damage like combat.
    /// </summary>
    public void ApplyRunMapDamage(int damage, Vector3 damageNumberWorldAnchor)
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.ApplyRunMapDamage: not in map-based run.");
            return;
        }

        if (damage <= 0)
            return;

        var reductionPercent = Mathf.Clamp(RelicActionRunner.QueryIntMax(RelicPhases.QueryMapCorruptionDamageReductionPercent), 0, 100);
        var finalDamage = Mathf.Max(0, Mathf.CeilToInt(damage * (1f - reductionPercent / 100f)));
        if (finalDamage <= 0)
            return;

        EnsureRunVitalityBaseline();
        _runCurrentHp = Mathf.Max(0, _runCurrentHp - finalDamage);
        NotifyRunVitalityChanged();

        CombatEvents.OnPlayerDamageNumber?.Invoke(finalDamage, damageNumberWorldAnchor);

        if (_runCurrentHp <= 0)
            EndRunFromPlayerDefeat();
    }

    private void NotifyRunVitalityChanged() => OnRunVitalityChanged?.Invoke();

    public void ApplyShrineMaxPowerBonus(int amount)
    {
        if (amount <= 0)
            return;
        _runShrineBonusMaxPower += amount;
        OnRunMaxPowerBudgetChanged?.Invoke();
    }

    /// <summary>Permanent run bonus: applied as Strength stacks at each combat start (see map strength definition).</summary>
    public void AddRunPermanentStrengthStacks(int amount)
    {
        if (amount <= 0)
            return;
        _runPermanentStrengthStacksFromSpecialEvents += amount;
    }

    /// <summary>Called from <see cref="CombatManager"/> after relic CombatStart so map-granted Strength persists every fight.</summary>
    public void TryApplyPermanentStrengthStacksAtCombatStart(CombatManager combat, PlayerStatus player, EnemyController enemy)
    {
        if (combat == null || player == null || _runPermanentStrengthStacksFromSpecialEvents <= 0 || mapRunPermanentStrengthDefinition == null)
            return;
        var ctx = new StatusEffectContext
        {
            CombatManager = combat,
            Player = player,
            Enemy = enemy,
        };
        player.StatusEffects.ApplyStatus(mapRunPermanentStrengthDefinition, _runPermanentStrengthStacksFromSpecialEvents, ctx);
    }

    public void HandleVictoryContinueFromCombat()
    {
        if (!_useMapBasedRun)
        {
            AdvanceToNextRoom();
            return;
        }

        var wasBoss = RunEncounterBuffer.TakeAndClearWasBossFight();
        if (wasBoss)
        {
            if (_currentActIndex >= 2)
            {
                _useMapBasedRun = false;
                EndRun();
                return;
            }

            _currentActIndex++;
            ClearMapPersistenceForNewAct();
            ClearMapEncounterDrawState();
            ClearMapSubsceneTransitionSnapshot();
            SceneManager.LoadScene(mapSceneName);
            return;
        }

        ReturnToMapFromSubScene();
    }

    private void EnsureRunVitalityBaseline()
    {
        if (_runVitalityInitialized)
            return;
        _runMaxHp = ReadStartingMaxHealthFromPlayerData();
        _runCurrentHp = _runMaxHp;
        _runVitalityInitialized = true;
    }

    static int ReadStartingMaxHealthFromPlayerData()
    {
        var pd = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        if (pd != null)
            return Mathf.Max(1, pd.startingMaxHealth);

        Debug.LogError("RunManager: PlayerDataContainer.Instance or RuntimeData is null — cannot read startingMaxHealth. Using 100.");
        return 100;
    }

    /// <summary>Act asset for the current map run act, or null if not in a map-based run.</summary>
    public MapActDefinitionSO GetCurrentMapActDefinitionOrNull()
    {
        if (!_useMapBasedRun || mapActDefinitionsByAct == null || mapActDefinitionsByAct.Length == 0)
            return null;
        var i = Mathf.Clamp(_currentActIndex, 0, mapActDefinitionsByAct.Length - 1);
        return mapActDefinitionsByAct[i];
    }

    private MapActDefinitionSO GetActDefinitionOrThrow()
    {
        if (mapActDefinitionsByAct == null || mapActDefinitionsByAct.Length == 0)
            throw new InvalidOperationException("RunManager: mapActDefinitionsByAct is not configured.");
        var i = Mathf.Clamp(_currentActIndex, 0, mapActDefinitionsByAct.Length - 1);
        var p = mapActDefinitionsByAct[i];
        if (p == null)
            throw new InvalidOperationException($"RunManager: mapActDefinitionsByAct[{i}] is null.");
        return p;
    }

    public void AdvanceToNextRoom()
    {
        if (HasNextRoom)
        {
            currentRoomIndex++;
            LoadCurrentRoom();
        }
        else
        {
            EndRun();
        }
    }

    private void LoadCurrentRoom()
    {
        var room = CurrentRoom;
        if (room == null) return;

        switch (room.roomType)
        {
            case RoomType.Combat:
                if (room.enemyType == null)
                {
                    Debug.LogError($"RunManager: Room {currentRoomIndex} is Combat but has no enemyType assigned!");
                    return;
                }
                SceneManager.LoadScene(combatSceneName);
                break;

            case RoomType.Shop:
                if (string.IsNullOrEmpty(shopSceneName))
                {
                    Debug.LogError("RunManager: shopSceneName is not assigned for Shop rooms.");
                    return;
                }
                SceneManager.LoadScene(shopSceneName);
                break;

            default:
                Debug.LogError($"RunManager: Unsupported room type '{room.roomType}' at index {currentRoomIndex}");
                break;
        }
    }

    private void EndRun()
    {
        Debug.Log("RunManager: Run complete!");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void EndRunFromPlayerDefeat()
    {
        Debug.LogError("RunManager: Game over — player HP reached 0.");
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
