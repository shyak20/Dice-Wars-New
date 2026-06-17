using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    public void StartNewGame()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("MainMenuController: RunManager not found in scene!");
            return;
        }

        RunManager.Instance.StartRun();
    }

    public void QuitGame()
    {
        // Use the full name to avoid the "ambiguous reference" error
        UnityEngine.Application.Quit();
    }
}