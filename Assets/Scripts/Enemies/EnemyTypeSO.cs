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

[System.Serializable]
public class EnemyPhaseDefinition
{
    [Tooltip("Used for debugging/inspector readability.")]
    public string phaseName = "Phase";
    [Tooltip("Health threshold to ENTER this phase. Phase 1 ignores this value.")]
    [Min(0)] public int phaseTargetHealth;
    [Tooltip("Actions used while this phase is active.")]
    public List<EnemyActionSO> actionCycle = new List<EnemyActionSO>();
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

    [Header("Intent cycle (legacy single-phase)")]
    public bool isSequential = true; // Set this to TRUE for your cycle
    public List<EnemyActionSO> actionCycle; // Legacy fallback when phases are not configured

    [Header("Intent phases (optional, up to 3)")]
    [Tooltip("If set, enemy uses phase action lists instead of legacy actionCycle. Phase 2/3 transition when HP <= their target health.")]
    [SerializeField] private List<EnemyPhaseDefinition> phases = new List<EnemyPhaseDefinition>();
    public IReadOnlyList<EnemyPhaseDefinition> Phases => phases;

    public bool HasConfiguredPhases => phases != null && phases.Count > 0 && phases[0] != null &&
                                       phases[0].actionCycle != null && phases[0].actionCycle.Count > 0;
}