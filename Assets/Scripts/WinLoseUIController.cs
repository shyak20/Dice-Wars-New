using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinLoseUIController : MonoBehaviour
{
    [Header("Victory")]
    [Tooltip("Post-combat win popup + rewards. If null, falls back to Face Reward only.")]
    [SerializeField] private WinStageFlowController winStageFlow;

    [Header("Face Reward (legacy if Win Stage not assigned)")]
    [SerializeField] private FaceRewardManager faceRewardManager;

    [Header("Victory — hide in scene")]
    [Tooltip("Set inactive as soon as victory is triggered (before win popup / face reward). Re-enable manually or reload the scene if needed.")]
    [SerializeField] private List<GameObject> disableOnVictoryScreen = new List<GameObject>();

    [Header("Defeat UI")]
    public GameObject gameOverPanel;
    public Button mainMenuButton;

    private void OnEnable()
    {
        CombatEvents.OnPlayerVictory += OnPlayerVictory;
        CombatEvents.OnPlayerDefeat += ShowGameOver;
        FaceRewardEvents.OnFaceRewardCompleted += OnFaceRewardCompleted;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerVictory -= OnPlayerVictory;
        CombatEvents.OnPlayerDefeat -= ShowGameOver;
        FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardCompleted;
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }
    }

    private void OnPlayerVictory()
    {
        DisableObjectsForVictoryScreen();

        if (winStageFlow != null)
        {
            winStageFlow.BeginVictoryFlow();
            return;
        }

        if (faceRewardManager == null)
        {
            Debug.LogError("WinLoseUIController: Assign Win Stage Flow or Face Reward Manager for victory.");
            return;
        }

        faceRewardManager.StartFaceReward();
    }

    private void OnFaceRewardCompleted(DieFaceSO face)
    {
        Debug.Log(face != null
            ? $"Face reward completed: {face.name} ({face.rarity})"
            : "Face reward flow closed (no face applied).");

        if (winStageFlow != null)
            return;

        if (RunManager.Instance != null)
        {
            if (RunManager.Instance.UseMapBasedRun)
                RunManager.Instance.HandleVictoryContinueFromCombat();
            else
                RunManager.Instance.AdvanceToNextRoom();
        }
        else
        {
            Debug.LogError("WinLoseUIController: RunManager not found! Falling back to main menu.");
            GoToMainMenu();
        }
    }

    private void ShowGameOver()
    {
        DisableObjectsForVictoryScreen();
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void GoToMainMenu()
    {
        RunEncounterBuffer.AbortPendingMapCombatState();
        if (RunManager.Instance != null)
            RunManager.Instance.LoadMainMenuScene();
        else
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

    private void DisableObjectsForVictoryScreen()
    {
        if (disableOnVictoryScreen == null) return;
        for (var i = 0; i < disableOnVictoryScreen.Count; i++)
        {
            var go = disableOnVictoryScreen[i];
            if (go != null)
                go.SetActive(false);
        }
    }
}