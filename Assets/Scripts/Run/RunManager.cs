using System;
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

    private MapGrid _persistedMapGrid;
    private Vector2Int _persistedPlayerCell;
    private int _persistedMovesTaken;
    private bool _hasPersistedMapState;

    private readonly System.Random _enemyDrawRng = new System.Random();
    private readonly Dictionary<EnemyRank, HashSet<EnemyTypeSO>> _drawnUniquesThisMap = new Dictionary<EnemyRank, HashSet<EnemyTypeSO>>();
    private readonly HashSet<UnknownMapEventSO> _drawnUnknownThisMap = new HashSet<UnknownMapEventSO>();
    private readonly List<EnemyTypeSO> _enemyDrawScratch = new List<EnemyTypeSO>();
    private readonly List<UnknownMapEventSO> _unknownValidScratch = new List<UnknownMapEventSO>();
    private readonly List<UnknownMapEventSO> _unknownUnusedScratch = new List<UnknownMapEventSO>();
    private readonly List<RelicSO> _runRelics = new List<RelicSO>();

    public bool UseMapBasedRun => _useMapBasedRun;
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

    /// <summary>Relics collected this run (shop, treasure, etc.).</summary>
    public IReadOnlyList<RelicSO> RunRelics => _runRelics;

    public event Action OnRunRelicsChanged;

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
    }

    public void StartRun()
    {
        if (RunEconomyManager.Instance != null)
            RunEconomyManager.Instance.ResetEconomyForNewRun();

        ClearRunRelics();
        _runVitalityInitialized = false;

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
        _runVitalityInitialized = false;
        ClearMapPersistenceForNewAct();
        ClearMapEncounterDrawState();
        currentRoomIndex = 0;

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

    public void ClearMapPersistenceForNewAct()
    {
        _persistedMapGrid = null;
        _hasPersistedMapState = false;
    }

    public void PersistAndLoadFightScene(MapGrid grid, Vector2Int playerCell, int movesTaken, EnemyRank rank,
        bool isBossEndTile)
    {
        if (!_useMapBasedRun)
        {
            Debug.LogError("RunManager.PersistAndLoadFightScene: not in map-based run.");
            return;
        }

        var enemy = DrawEnemyForMapCombat(rank);
        SaveMapPersistence(grid, playerCell, movesTaken);
        RunEncounterBuffer.SetPendingCombat(enemy, isBossEndTile);
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
        SceneManager.LoadScene(shopSceneName);
    }

    public void ReturnToMapFromSubScene()
    {
        if (string.IsNullOrEmpty(mapSceneName))
        {
            Debug.LogError("RunManager: mapSceneName is not assigned.");
            return;
        }

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
    }

    /// <summary>
    /// Picks an unknown event for the current act (unique entries preferred until the pool is exhausted).
    /// Returns null if the act defines no unknown events.
    /// </summary>
    public UnknownMapEventSO DrawUnknownMapEvent()
    {
        var act = GetActDefinitionOrThrow();
        _unknownValidScratch.Clear();
        if (act.possibleUnknownEvents != null)
        {
            foreach (var u in act.possibleUnknownEvents)
            {
                if (u != null)
                    _unknownValidScratch.Add(u);
            }
        }

        if (_unknownValidScratch.Count == 0)
            return null;

        _unknownUnusedScratch.Clear();
        foreach (var u in _unknownValidScratch)
        {
            if (!_drawnUnknownThisMap.Contains(u))
                _unknownUnusedScratch.Add(u);
        }

        UnknownMapEventSO pick;
        if (_unknownUnusedScratch.Count > 0)
        {
            pick = _unknownUnusedScratch[_enemyDrawRng.Next(_unknownUnusedScratch.Count)];
            _drawnUnknownThisMap.Add(pick);
        }
        else
        {
            pick = _unknownValidScratch[_enemyDrawRng.Next(_unknownValidScratch.Count)];
        }

        return pick;
    }

    public void ApplyRunVitalityToPlayerIfAny(PlayerStatus player)
    {
        if (player == null || !_runVitalityInitialized)
            return;
        player.ApplyRunVitality(_runCurrentHp, _runMaxHp);
    }

    public void CaptureRunVitalityFromPlayer(PlayerStatus player)
    {
        if (player == null)
            return;
        _runCurrentHp = player.GetCurrentHealth();
        _runMaxHp = player.maxHealth;
        _runVitalityInitialized = true;
    }

    public void ApplyShrineHeal(int amount)
    {
        if (amount <= 0)
            return;
        EnsureRunVitalityBaseline();
        _runCurrentHp = Mathf.Min(_runCurrentHp + amount, _runMaxHp);
        NotifyRunVitalityChanged();
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

        EnsureRunVitalityBaseline();
        _runCurrentHp = Mathf.Max(0, _runCurrentHp - damage);
        NotifyRunVitalityChanged();

        CombatEvents.OnPlayerDamageNumber?.Invoke(damage, damageNumberWorldAnchor);

        if (_runCurrentHp <= 0)
            EndRunFromPlayerDefeat();
    }

    private void NotifyRunVitalityChanged() => OnRunVitalityChanged?.Invoke();

    public void ApplyShrineMaxPowerBonus(int amount)
    {
        if (amount <= 0)
            return;
        _runShrineBonusMaxPower += amount;
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
