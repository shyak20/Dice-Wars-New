using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Clears saved rank/trial progression when a UI <see cref="Button"/> is pressed.
/// Uses <see cref="ProgressionManager.ClearAllSavedProgress"/> (all characters, PlayerPrefs progression keys).
/// </summary>
public sealed class ResetPlayerProgressionButton : MonoBehaviour
{
    [SerializeField] private Button button;

    [Tooltip("When enabled, also calls PlayerPrefs.DeleteAll() (music volume, etc.), not only progression.")]
    [SerializeField] private bool deleteAllPlayerPrefs;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError(
                $"ResetPlayerProgressionButton on '{name}': assign button or add this component to a Button GameObject.",
                this);
            return;
        }

        button.onClick.AddListener(OnResetClicked);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnResetClicked);
    }

    void OnResetClicked() => ResetProgression();

    /// <summary>Callable from a Button OnClick or UnityEvent without this component on the button object.</summary>
    public void ResetProgression()
    {
        if (deleteAllPlayerPrefs)
            PlayerSaveReset.DeleteAllPlayerPrefsAndProgression();
        else
            ProgressionManager.ClearAllSavedProgress();
    }
}
