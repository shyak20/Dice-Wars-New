using UnityEngine;

[CreateAssetMenu(fileName = "NewAction", menuName = "DiceGame/EnemyAction")]
public class EnemyActionSO : ScriptableObject
{
    public string actionName;
    public int damage;
    public int numberOfAttacks = 1; // Default to 1 hit
    public int armor;
}