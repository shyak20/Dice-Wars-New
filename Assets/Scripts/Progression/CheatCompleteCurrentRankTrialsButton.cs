using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug UI: completes all incomplete trials on the active rank when pressed.
/// Add to a GameObject with a <see cref="Button"/>. On Dice Select, assign
/// <see cref="diceSelectScene"/> or select a character first so progression is bound.
/// </summary>
public sealed class CheatCompleteCurrentRankTrialsButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Optional. When set, uses the character currently previewed on Dice Select.")]
    [SerializeField] private DiceSelectSceneController diceSelectScene;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError(
                $"{nameof(CheatCompleteCurrentRankTrialsButton)} on '{name}': assign a Button or put this on a Button object.",
                this);
            return;
        }

        button.onClick.AddListener(OnPressed);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnPressed);
    }

    void OnPressed()
    {
        var progression = ProgressionManager.TryGetRuntime();
        if (progression == null)
        {
            Debug.LogError(
                $"{nameof(CheatCompleteCurrentRankTrialsButton)}: {nameof(ProgressionManager)} missing.",
                this);
            return;
        }

        if (!TryResolveCharacter(progression, out var character))
        {
            Debug.LogError(
                $"{nameof(CheatCompleteCurrentRankTrialsButton)}: no character to cheat. Select a character on Dice Select or assign progression first.",
                this);
            return;
        }

        if (character.progressionCatalog == null)
        {
            Debug.LogError(
                $"{nameof(CheatCompleteCurrentRankTrialsButton)}: '{character.DisplayName}' has no progressionCatalog.",
                character);
            return;
        }

        if (!progression.IsInitializedFor(character))
            progression.InitializeForCharacter(character);

        var completed = progression.CheatCompleteAllActiveRankTrials();
        if (completed > 0)
            Debug.Log(
                $"Cheat: completed {completed} trial(s) on current rank for '{character.DisplayName}'.",
                this);
    }

    bool TryResolveCharacter(ProgressionManager progression, out PlayerDataSO character)
    {
        if (diceSelectScene != null && diceSelectScene.TryGetPreviewCharacter(out character))
            return true;

        character = progression.ActiveCharacterTemplate;
        if (character != null)
            return true;

        var container = PlayerDataContainer.Instance;
        if (container?.ActiveCharacterTemplate != null)
        {
            character = container.ActiveCharacterTemplate;
            return true;
        }

        character = null;
        return false;
    }
}
