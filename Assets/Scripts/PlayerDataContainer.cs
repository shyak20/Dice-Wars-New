using UnityEngine;

public class PlayerDataContainer : MonoBehaviour
{
    [SerializeField] private PlayerDataSO sourcePlayerData;

    public static PlayerDataContainer Instance { get; private set; }
    public PlayerDataSO RuntimeData { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sourcePlayerData == null)
        {
            Debug.LogError("PlayerDataContainer: sourcePlayerData is not assigned!");
            return;
        }

        CloneDeckForRuntime();
    }

    private void CloneDeckForRuntime()
    {
        RuntimeData = Instantiate(sourcePlayerData);
        for (var d = 0; d < RuntimeData.currentDeck.Count; d++)
        {
            var clonedDie = Instantiate(RuntimeData.currentDeck[d]);
            clonedDie.name = RuntimeData.currentDeck[d].name;
            RuntimeData.currentDeck[d] = clonedDie;
        }
    }
}
