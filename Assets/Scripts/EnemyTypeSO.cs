using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "DiceGame/EnemyType")]
public class EnemyTypeSO : ScriptableObject
{
    public string enemyName;
    public int maxHealth;

    [Tooltip("The list of moves this enemy can choose from each turn.")]
    public List<EnemyActionSO> availableActions;
}