using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewPlayerData", menuName = "DiceGame/PlayerData")]
public class PlayerDataSO : ScriptableObject
{
    [Header("Character")]
    [Tooltip("Shown on the dice-select screen and other character pickers.")]
    [SerializeField] private string characterDisplayName;
    [Tooltip("Shown on the lose screen UI for this character (e.g. LoseScreen component).")]
    [SerializeField] private Sprite loseScreenImage;
    [Tooltip("Left hand art in the fight scene (e.g. FightCharacterHandImages).")]
    [SerializeField] private Sprite leftHandImage;
    [Tooltip("Right hand art in the fight scene (e.g. FightCharacterHandImages).")]
    [SerializeField] private Sprite rightHandImage;
    [TextArea(2, 6)]
    [Tooltip("Flavor or mechanical summary shown when this character is selected on the dice-select screen.")]
    [SerializeField] private string description;
    [Tooltip("Stable id for meta upgrade save data. Defaults to this asset's name when empty.")]
    [SerializeField] private string metaSaveId;

    public string DisplayName => string.IsNullOrWhiteSpace(characterDisplayName) ? name : characterDisplayName.Trim();
    public Sprite LoseScreenImage => loseScreenImage;
    public Sprite LeftHandImage => leftHandImage;
    public Sprite RightHandImage => rightHandImage;
    public string Description => description ?? string.Empty;
    public string MetaSaveId => string.IsNullOrWhiteSpace(metaSaveId) ? name : metaSaveId.Trim();

    [Tooltip("Base starting dice for this character (authored on the asset). Extra dice from progression Add Starting Die rewards are stored in PlayerPrefs per character, not here.")]
    public List<DieAssetSO> currentDeck = new List<DieAssetSO>();

    [Header("Vitality")]
    [Tooltip("Starting and default max HP for a new run (before relics/shrines). Applied to PlayerStatus when combat initializes.")]
    [Min(1)] public int startingMaxHealth = 100;

    [Header("Combat Settings")]
    public int maxRollsPerTurn = 3;

    [Tooltip("Base max power before deck slots 3+, shrine bonus, and relic query modifiers.")]
    [Min(1)] public int baseMaxPower = 12;

    [Header("Map")]
    [Tooltip("Moves allowed per map before corruption / overflow damage (see MapMovementManager).")]
    [Min(1)] public int moveLimit = 8;

    [Tooltip("Perfect strike pool multiplier when no higher relic multiplier applies (overcharge stacks add on top).")]
    [Min(1)] public int perfectStrikeBaseMultiplier = 2;

    [Header("Progression")]
    [Tooltip("Per-character rank/trial ladder. Required for meta progression on this character.")]
    public ProgressionCatalogSO progressionCatalog;
}