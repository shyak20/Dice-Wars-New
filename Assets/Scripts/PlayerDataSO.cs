using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewPlayerData", menuName = "DiceGame/PlayerData")]
public class PlayerDataSO : ScriptableObject
{
    [Header("Character")]
    [Tooltip("Shown on the dice-select screen and other character pickers.")]
    [SerializeField] private string characterDisplayName;
    [Tooltip("Large portrait shown in the dice-select preview panel.")]
    [SerializeField] private Sprite portrait;
    [Tooltip("Compact portrait shown on each character button in the dice-select screen.")]
    [SerializeField] private Sprite smallPortrait;
    [TextArea(2, 6)]
    [Tooltip("Flavor or mechanical summary shown when this character is selected on the dice-select screen.")]
    [SerializeField] private string description;
    [Tooltip("Stable id for meta upgrade save data. Defaults to this asset's name when empty.")]
    [SerializeField] private string metaSaveId;

    public string DisplayName => string.IsNullOrWhiteSpace(characterDisplayName) ? name : characterDisplayName.Trim();
    public Sprite Portrait => portrait;
    public Sprite SmallPortrait => smallPortrait != null ? smallPortrait : portrait;
    public string Description => description ?? string.Empty;
    public string MetaSaveId => string.IsNullOrWhiteSpace(metaSaveId) ? name : metaSaveId.Trim();

    [Tooltip("Dice always included in this character's starting deck.")]
    public List<DieAssetSO> currentDeck = new List<DieAssetSO>();

    [Tooltip("Extra starting dice unlocked one at a time via meta upgrades (see Character Meta Upgrade Prices SO).")]
    public List<DieAssetSO> lockedStartingDice = new List<DieAssetSO>();

    [Header("Vitality")]
    [Tooltip("Starting and default max HP for a new run (before relics/shrines). Applied to PlayerStatus when combat initializes.")]
    [Min(1)] public int startingMaxHealth = 100;

    [Header("Combat Settings")]
    public int maxRollsPerTurn = 3;

    [Tooltip("Base max power before deck slots 3+, shrine bonus, and relic query modifiers.")]
    [Min(1)] public int baseMaxPower = 12;

    [Tooltip("Perfect strike pool multiplier when no higher relic multiplier applies (overcharge stacks add on top).")]
    [Min(1)] public int perfectStrikeBaseMultiplier = 2;
}