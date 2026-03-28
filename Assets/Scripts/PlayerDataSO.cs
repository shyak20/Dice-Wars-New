using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewPlayerData", menuName = "DiceGame/PlayerData")]
public class PlayerDataSO : ScriptableObject
{
    public List<DieAssetSO> currentDeck = new List<DieAssetSO>();

    [Header("Combat Settings")]
    public int maxRollsPerTurn = 3;
}