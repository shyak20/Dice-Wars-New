using UnityEngine;

[System.Serializable]
public class RoomDefinition
{
    public RoomType roomType;

    [Header("Combat")]
    public EnemyTypeSO enemyType;
}
