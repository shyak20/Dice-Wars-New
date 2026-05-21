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

        MetaCharacterUpgradeManager.OnCharacterUpgradesChanged += OnCharacterUpgradesChanged;
    }

    void OnDisable()
    {
        if (sceneController != null)
            sceneController.CharacterPreviewChanged -= OnCharacterPreviewChanged;

        MetaCharacterUpgradeManager.OnCharacterUpgradesChanged -= OnCharacterUpgradesChanged;
    }

    void Start()
    {
        if (sceneController != null && sceneController.TryGetPreviewCharacter(out var character))
            Refresh(character);
    }

    void OnCharacterPreviewChanged(PlayerDataSO character) => Refresh(character);

    void OnCharacterUpgradesChanged(PlayerDataSO character)
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

        var upgrades = MetaCharacterUpgradeManager.TryGetRuntime();

        if (maxHealthText != null)
        {
            var maxHealth = upgrades != null
                ? upgrades.GetEffectiveStartingMaxHealth(character)
                : Mathf.Max(1, character.startingMaxHealth);
            maxHealthText.text = maxHealth.ToString();
        }

        if (baseMaxPowerText != null)
        {
            var baseMaxPower = upgrades != null
                ? upgrades.GetEffectiveBaseMaxPower(character)
                : Mathf.Max(1, character.baseMaxPower);
            baseMaxPowerText.text = baseMaxPower.ToString();
        }

        if (moveLimitText != null)
            moveLimitText.text = Mathf.Max(1, character.moveLimit).ToString();
    }
}
