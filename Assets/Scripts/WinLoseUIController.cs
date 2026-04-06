using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinLoseUIController : MonoBehaviour
{
    [Header("Face Reward")]
    [SerializeField] private FaceRewardManager faceRewardManager;

    [Header("Defeat UI")]
    public GameObject gameOverPanel;
    public Button mainMenuButton;

    private void OnEnable()
    {
        CombatEvents.OnPlayerVictory += StartFaceReward;
        CombatEvents.OnPlayerDefeat += ShowGameOver;
        FaceRewardEvents.OnFaceRewardCompleted += OnFaceRewardCompleted;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerVictory -= StartFaceReward;
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

    private void StartFaceReward()
    {
        if (faceRewardManager == null)
        {
            Debug.LogError("WinLoseUIController: faceRewardManager is not assigned!");
            return;
        }

        faceRewardManager.StartFaceReward();
    }

    private void OnFaceRewardCompleted(DieFaceSO face)
    {
        Debug.Log(face != null
            ? $"Face reward completed: {face.name} ({face.rarity})"
            : "Face reward flow closed (no face applied).");

        if (RunManager.Instance != null)
        {
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
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}