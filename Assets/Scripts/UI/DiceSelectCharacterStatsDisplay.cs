using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Dice-select preview for the selected character's max health, base max power, and map move limit.
/// </summary>
public sealed class DiceSelectCharacterStatsDisplay : MonoBehaviour
{
    [SerializeField] private DiceSelectSceneController sceneController;
    [SerializeField] private TMP_Text maxHealthText;
    [SerializeField] private TMP_Text baseMaxPowerText;
    [SerializeField] private TMP_Text moveLimitText;

    void Awake()
    {
        if (sceneController == null)
            sceneController = FindObjectOfType<DiceSelectSceneController>(true);

        if (sceneController == null)
            Debug.LogError("DiceSelectCharacterStatsDisplay: assign sceneController.", this);
    }

    void OnEnable()
    {
        if (sceneController != null)
            sceneController.CharacterPreviewChanged += OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged += OnCharacterProgressionChanged;
    }

    void OnDisable()
    {
        if (sceneController != null)
            sceneController.CharacterPreviewChanged -= OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged -= OnCharacterProgressionChanged;
    }

    void Start()
    {
        if (sceneController != null && sceneController.TryGetPreviewCharacter(out var character))
            Refresh(character);
    }

    void OnCharacterPreviewChanged(PlayerDataSO character) => Refresh(character);

    void OnCharacterProgressionChanged(PlayerDataSO character)
    {
        if (sceneController == null || !sceneController.TryGetPreviewCharacter(out var preview))
            return;

        if (preview == character)
            Refresh(preview);
    }

    void Refresh(PlayerDataSO character)
    {
        if (character == null)
            return;

        var progression = ProgressionManager.TryGetRuntime();

        if (maxHealthText != null)
        {
            var maxHealth = Mathf.Max(1, character.startingMaxHealth
                + (progression != null ? progression.GetStartingHPModifier() : 0));
            maxHealthText.text = maxHealth.ToString();
        }

        if (baseMaxPowerText != null)
        {
            var baseMaxPower = Mathf.Max(1, character.baseMaxPower
                + (progression != null ? progression.GetMaxPowerModifier() : 0));
            baseMaxPowerText.text = baseMaxPower.ToString();
        }

        if (moveLimitText != null)
        {
            var moves = Mathf.Max(1, character.moveLimit
                + (progression != null ? progression.GetGridMoveModifier() : 0));
            moveLimitText.text = moves.ToString();
        }
    }
}
