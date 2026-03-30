using UnityEngine;
using UnityEngine.SceneManagement;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [SerializeField] private EncounterListSO encounterList;
    [SerializeField] private string combatSceneName = "FightScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private int currentRoomIndex;

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

    public bool HasNextRoom => currentRoomIndex < encounterList.rooms.Count - 1;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (encounterList == null)
            Debug.LogError("RunManager: encounterList is not assigned!");
    }

    public void StartRun()
    {
        if (encounterList == null || encounterList.rooms.Count == 0)
        {
            Debug.LogError("RunManager: Cannot start run — encounter list is empty or null!");
            return;
        }

        currentRoomIndex = 0;
        LoadCurrentRoom();
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
}
