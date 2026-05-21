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

public enum EnemyResistanceElement
{
    Physical,
    Fire,
    Ice,
    Nature
}

[System.Serializable]
public class EnemyStartingResistance
{
    public EnemyResistanceElement element = EnemyResistanceElement.Physical;
    [Range(0f, 100f)]
    public float lessDamagePercent = 0f;
}

[System.Serializable]
public class EnemyValueRolledListener
{
    [Tooltip("Triggers when a resolved face value matches one of these numbers.")]
    public List<int> rolledValues = new List<int>();

    [Tooltip("Icon shown in enemy status bar for this listener.")]
    public Sprite icon;

    [Tooltip("Optional. Hover title for this value-roll listener.")]
    public string title;
    [TextArea(2, 5)]
    [Tooltip("Optional. Hover body text for this value-roll listener.")]
    public string description;

    [Tooltip("Executes immediately when the condition matches.")]
    [SerializeReference]
    public List<IGameAction> actions = new List<IGameAction>();
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

    [Header("Map draw")]
    [Tooltip("When true, this enemy cannot be randomly drawn for the first combat encounter of each act they appear in.")]
    public bool excludeFromFirstFight = true;

    public int maxHealth;
    public int startArmor;

    [Header("Rewards")]
    [Tooltip("Minimum gold granted when this enemy is defeated (run currency).")]
    [Min(0)] public int minGoldReward;
    [Tooltip("Maximum gold granted when this enemy is defeated (run currency).")]
    [Min(0)] public int maxGoldReward;
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

    [Header("Starting Buffs")]
    [Tooltip("Flat damage resistance by incoming damage element.")]
    public List<EnemyStartingResistance> startingResistances = new List<EnemyStartingResistance>();
    [Tooltip("Immediate action triggers when player resolves matching rolled values.")]
    public List<EnemyValueRolledListener> valueRolledListeners = new List<EnemyValueRolledListener>();

    public bool HasConfiguredPhases => phases != null && phases.Count > 0 && phases[0] != null &&
                                       phases[0].actionCycle != null && phases[0].actionCycle.Count > 0;

    public int RollGoldReward()
    {
        var min = Mathf.Max(0, minGoldReward);
        var max = Mathf.Max(min, maxGoldReward);
        return Random.Range(min, max + 1);
    }
}
