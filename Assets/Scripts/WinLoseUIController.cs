using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinLoseUIController : MonoBehaviour
{
    [Header("Data Source")]
    public FaceLootTableSO lootTable;

    [Header("Victory/Reward UI")]
    public GameObject rewardPanel;
    public Transform rewardContainer;
    public GameObject rewardButtonPrefab; // This prefab needs the RewardButtonUI script!

    [Header("Defeat UI")]
    public GameObject gameOverPanel;
    public Button mainMenuButton;

    private void OnEnable()
    {
        CombatEvents.OnPlayerVictory += ShowRewardScreen;
        CombatEvents.OnPlayerDefeat += ShowGameOver;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerVictory -= ShowRewardScreen;
        CombatEvents.OnPlayerDefeat -= ShowGameOver;
    }

    private void Start()
    {
        if (rewardPanel != null) rewardPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }
    }

    private void ShowRewardScreen()
    {
        if (rewardPanel == null || lootTable == null) return;
        rewardPanel.SetActive(true);

        foreach (Transform child in rewardContainer) Destroy(child.gameObject);

        // Get 3 random unique rewards from our new Loot Table
        List<DieFaceSO> rewards = lootTable.GetRandomRewards(3);

        foreach (DieFaceSO face in rewards)
        {
            GameObject btnObj = Instantiate(rewardButtonPrefab, rewardContainer);
            RewardButtonUI script = btnObj.GetComponent<RewardButtonUI>();

            if (script != null)
            {
                script.Setup(face, SelectReward);
            }
        }
    }

    private void SelectReward(DieFaceSO face)
    {
        // Explicitly use UnityEngine.Debug to avoid ambiguity with System.Diagnostics
        UnityEngine.Debug.Log($"Player picked: {face.name} ({face.rarity})");

        // Add logic here later to save 'face' to player inventory
        GoToMainMenu();
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