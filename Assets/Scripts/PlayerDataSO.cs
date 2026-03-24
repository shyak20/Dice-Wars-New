using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewPlayerData", menuName = "DiceGame/PlayerData")]
public class PlayerDataSO : ScriptableObject
{
    public List<DieAssetSO> currentDeck = new List<DieAssetSO>();

    // You can add Gold, Health, etc., here later.
}