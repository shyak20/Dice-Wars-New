using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEncounterList", menuName = "DiceGame/EncounterList")]
public class EncounterListSO : ScriptableObject
{
    public List<RoomDefinition> rooms = new List<RoomDefinition>();
}
