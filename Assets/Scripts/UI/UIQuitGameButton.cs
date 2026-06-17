using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quits the application from a UI button.
/// In the editor, stops Play mode for quick testing.
/// </summary>
public sealed class UIQuitGameButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError($"UIQuitGameButton on '{name}': assign a Button or place this on a Button object.", this);
            return;
        }

        button.onClick.AddListener(QuitGame);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(QuitGame);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
