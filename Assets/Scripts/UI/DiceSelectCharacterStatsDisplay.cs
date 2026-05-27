using TMPro;
using UnityEngine;

/// <summary>
/// Dice-select preview for the selected character's rank, max health, base max power, map move limit, and max rolls per turn.
/// Rank uses a <see cref="TMP_Text"/>; other stats use <see cref="CharacterInfoStat"/> rows.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class DiceSelectCharacterStatsDisplay : MonoBehaviour
{
    [SerializeField] private DiceSelectSceneController sceneController;
    [SerializeField] private GameIconIndexSO gameIconIndex;
    [SerializeField] private TMP_Text rankNameText;
    [SerializeField] private CharacterInfoStat maxHealthStat;
    [SerializeField] private CharacterInfoStat baseMaxPowerStat;
    [SerializeField] private CharacterInfoStat moveLimitStat;
    [SerializeField] private CharacterInfoStat maxRollsPerTurnStat;

    void Awake()
    {
        if (sceneController == null)
            sceneController = FindObjectOfType<DiceSelectSceneController>(true);

        if (sceneController == null)
            Debug.LogError("DiceSelectCharacterStatsDisplay: assign sceneController.", this);

        maxHealthStat?.SetMainAttributeIconId(MainAttributeIconId.Hp);
        baseMaxPowerStat?.SetMainAttributeIconId(MainAttributeIconId.Power);
        moveLimitStat?.SetMainAttributeIconId(MainAttributeIconId.Movement);
        maxRollsPerTurnStat?.SetMainAttributeIconId(MainAttributeIconId.ExtraRoll);
    }

    void OnEnable()
    {
        if (sceneController != null)
            sceneController.CharacterPreviewChanged += OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged += OnCharacterProgressionChanged;
        DiceSelectProgressionDisplayGate.DeferredRefreshRequested += OnDeferredProgressionRefreshRequested;
    }

    void Start()
    {
        if (sceneController != null && sceneController.TryGetPreviewCharacter(out var character))
            TryRefresh(character);
    }

    void OnDisable()
    {
        if (sceneController != null)
            sceneController.CharacterPreviewChanged -= OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged -= OnCharacterProgressionChanged;
        DiceSelectProgressionDisplayGate.DeferredRefreshRequested -= OnDeferredProgressionRefreshRequested;
    }

    void OnCharacterPreviewChanged(PlayerDataSO character) => TryRefresh(character);

    void OnCharacterProgressionChanged(PlayerDataSO character)
    {
        if (sceneController == null || !sceneController.TryGetPreviewCharacter(out var preview))
            return;

        if (preview == character)
            TryRefresh(preview);
    }

    void OnDeferredProgressionRefreshRequested()
    {
        if (sceneController != null && sceneController.TryGetPreviewCharacter(out var character))
            Refresh(character);
    }

    void TryRefresh(PlayerDataSO character)
    {
        if (!DiceSelectProgressionDisplayGate.ShouldRefreshProgressionDisplays())
            return;

        Refresh(character);
    }

    void Refresh(PlayerDataSO character)
    {
        if (character == null)
        {
            if (rankNameText != null)
                rankNameText.text = string.Empty;
            var iconIndex = ResolveIconIndex();
            ClearStat(maxHealthStat, iconIndex);
            ClearStat(baseMaxPowerStat, iconIndex);
            ClearStat(moveLimitStat, iconIndex);
            ClearStat(maxRollsPerTurnStat, iconIndex);
            return;
        }

        var icons = ResolveIconIndex();

        var progression = ProgressionManager.TryGetRuntime();
        if (progression == null && character.progressionCatalog != null)
            progression = ProgressionManager.EnsureRuntime(character.progressionCatalog);

        if (progression != null && !progression.IsInitializedFor(character))
            progression.InitializeForCharacter(character);

        if (rankNameText != null)
        {
            var rank = progression != null ? progression.GetActiveRank() : null;
            rankNameText.text = FormatRankName(rank);
        }

        var maxHealth = Mathf.Max(1, character.startingMaxHealth
            + (progression != null ? progression.GetStartingHPModifier() : 0));
        SetStat(maxHealthStat, maxHealth.ToString(), icons);

        var baseMaxPower = Mathf.Max(1, character.baseMaxPower
            + (progression != null ? progression.GetMaxPowerModifier() : 0));
        SetStat(baseMaxPowerStat, baseMaxPower.ToString(), icons);

        var moves = Mathf.Max(1, character.moveLimit
            + (progression != null ? progression.GetGridMoveModifier() : 0));
        SetStat(moveLimitStat, moves.ToString(), icons);

        var maxRolls = Mathf.Max(1, character.maxRollsPerTurn
            + (progression != null ? progression.GetMaxRollsModifier() : 0));
        SetStat(maxRollsPerTurnStat, maxRolls.ToString(), icons);
    }

    GameIconIndexSO ResolveIconIndex() =>
        gameIconIndex != null ? gameIconIndex : GameIconCatalog.Active;

    static void SetStat(CharacterInfoStat stat, string value, GameIconIndexSO iconIndex)
    {
        if (stat != null)
            stat.SetValue(value, iconIndex);
    }

    static void ClearStat(CharacterInfoStat stat, GameIconIndexSO iconIndex)
    {
        if (stat != null)
            stat.ClearValue(iconIndex);
    }

    static string FormatRankName(PlayerRankSO rank)
    {
        if (rank == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(rank.rankName) ? $"Rank {rank.rankIndex}" : rank.rankName;
    }
}
