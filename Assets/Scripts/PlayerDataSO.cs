using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewPlayerData", menuName = "DiceGame/PlayerData")]
public class PlayerDataSO : ScriptableObject
{
    public List<DieAssetSO> currentDeck = new List<DieAssetSO>();

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