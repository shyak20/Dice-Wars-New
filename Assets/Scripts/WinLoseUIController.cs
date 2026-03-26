using System.Collections.Generic;
using System.Linq; // Needed for the weighted reward math
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinLoseUIController : MonoBehaviour
{
    [Header("Victory/Reward UI")]
    public GameObject rewardPanel;
    public Transform rewardContainer;
    public GameObject rewardButtonPrefab;
    [Tooltip("Drag every DieFaceSO in the game here so the system can pick from them")]
    public List<DieFaceSO> allPossibleFaces;

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
        if (rewardPanel == null) return;
        rewardPanel.SetActive(true);

        // Generate 3 unique rewards based on rarity weights
        List<DieFaceSO> rewards = GenerateRarityRewards(3);

        foreach (Transform child in rewardContainer) Destroy(child.gameObject);

        foreach (var face in rewards)
        {
            GameObject btnObj = Instantiate(rewardButtonPrefab, rewardContainer);

            // Set the text to show Value, Type, and Rarity
            TMPro.TMP_Text txt = btnObj.GetComponentInChildren<TMPro.TMP_Text>();
            if (txt != null) txt.text = $"{face.value} {face.type}\n<size=80%>{face.rarity}</size>";

            btnObj.GetComponent<Button>().onClick.AddListener(() => SelectReward(face));
        }
    }

    private List<DieFaceSO> GenerateRarityRewards(int count)
    {
        List<DieFaceSO> selected = new List<DieFaceSO>();
        if (allPossibleFaces == null || allPossibleFaces.Count == 0) return selected;

        for (int i = 0; i < count; i++)
        {
            // Calculate total weight of all possible faces
            int totalWeight = allPossibleFaces.Sum(f => f.spawnWeight);

            // FIXED: Explicitly use UnityEngine.Random to solve the ambiguity error
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var face in allPossibleFaces)
            {
                currentWeight += face.spawnWeight;
                if (roll < currentWeight)
                {
                    selected.Add(face);
                    break;
                }
            }
        }
        return selected;
    }

    private void SelectReward(DieFaceSO face)
    {
        // Add the face to the player's permanent collection logic here
        Debug.Log($"Collected new face: {face.name}");
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