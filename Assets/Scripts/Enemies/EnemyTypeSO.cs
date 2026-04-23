using UnityEngine;
using System.Collections.Generic;

public enum EnemyRank
{
    Normal,
    Elite,
    Boss
}

public enum EnemyBonusRewardPool
{
    DieFaces,
    Dice,
    Relics,
    Gems
}

[System.Serializable]
public class EnemyBonusRewardDrop
{
    public EnemyBonusRewardPool pool;
    [Range(0f, 1f)] public float dropChance = 0.2f;
    [Min(1)] public int rolls = 1;
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
    [Tooltip("Optional: extra reward rolls when this enemy dies.")]
    public List<EnemyBonusRewardDrop> additionalRewardDrops = new List<EnemyBonusRewardDrop>();
    [Tooltip("Pool used by additionalRewardDrops when pool = DieFaces.")]
    public FaceLootTableSO faceRewardPool;
    [Tooltip("Pool used by additionalRewardDrops when pool = Dice.")]
    public DieLootTableSO dieRewardPool;
    [Tooltip("Pool used by additionalRewardDrops when pool = Relics.")]
    public RelicLootTableSO relicRewardPool;
    [Tooltip("Pool used by additionalRewardDrops when pool = Gems.")]
    public GemLootTableSO gemRewardPool;

    public bool isSequential = true; // Set this to TRUE for your cycle
    public List<EnemyActionSO> actionCycle; // Rename for clarity
}