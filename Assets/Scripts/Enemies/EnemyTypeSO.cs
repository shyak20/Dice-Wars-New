using UnityEngine;
using System.Collections.Generic;

public enum EnemyRank
{
    Normal,
    Elite,
    Boss
}

[CreateAssetMenu(fileName = "NewEnemy", menuName = "DiceGame/EnemyType")]
public class EnemyTypeSO : ScriptableObject
{
    public string enemyName;

    [Header("Visual")]
    [Tooltip("Optional. Applied to the combat SpriteRenderer when this enemy type loads.")]
    public Sprite displaySprite;

    [Header("Combat animator")]
    [Tooltip("Optional. Assigned to the enemy's Animator at runtime; drives idle and per-intent states from EnemyActionSO.")]
    public RuntimeAnimatorController combatAnimatorController;

    [Tooltip("State name on the controller (e.g. Idle or Base Layer.Idle). Empty uses the controller default entry state.")]
    public string idleAnimatorStateName;

    [Header("Classification")]
    public EnemyRank enemyRank = EnemyRank.Normal;

    public int maxHealth;
    public int startArmor;

    [Header("Rewards")]
    [Tooltip("Gold granted when this enemy is defeated (run currency).")]
    public int goldReward;

    public bool isSequential = true; // Set this to TRUE for your cycle
    public List<EnemyActionSO> actionCycle; // Rename for clarity
}