using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public void StartNewGame()
    {
        // Reset player data here if necessary (HP, Starter Dice)
        SceneManager.LoadScene("FightScene");
    }

    public void QuitGame()
    {
        // Use the full name to avoid the "ambiguous reference" error
        UnityEngine.Application.Quit();
    }
}