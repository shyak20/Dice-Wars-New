using System.Collections.Generic;

public class GameActionContext
{
    public CombatManager CombatManager { get; set; }
    public PlayerStatus Player { get; set; }
    public EnemyController Enemy { get; set; }
    public List<FaceResult> ChanneledFaces { get; set; }
    public FaceResult TriggeringFace { get; set; }
    public PlayerDataSO PlayerData { get; set; }

    /// <summary>Set when <see cref="IGameAction.Execute"/> runs from an <see cref="EnemyActionSO"/> during the enemy turn.</summary>
    public EnemyActionSO SourceEnemyAction { get; set; }

    /// <summary>When running relic actions: which lifecycle hook is executing (see <see cref="RelicPhases"/>).</summary>
    public string RelicPhase { get; set; }

    public RelicSO SourceRelic { get; set; }
    public RelicRuntimeState RelicRuntime { get; set; }

    /// <summary>Relic queries: sum or max contributions depending on phase.</summary>
    public int RelicIntAccumulator { get; set; }

    public bool RelicBoolAccumulator { get; set; }

    /// <summary>Current player power meter (for relic hooks after a roll).</summary>
    public int CurrentPower { get; set; }

    public int MaxPower { get; set; }

    /// <summary>
    /// Set during turn-end execution for deferred faces: stack counts resolved from the element pool (Perfect Strike / bust adjusted).
    /// </summary>
    public Dictionary<ApplyStatusEffectAction, int> PendingApplyStackOverrides { get; set; }
}
