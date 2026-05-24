using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns <see cref="ProgressionTrialSlotUI"/> for the active rank and shows rank completion on a slider + X/Y label.
/// </summary>
public sealed class ProgressionTrialsPanelUI : MonoBehaviour
{
    [SerializeField] private DiceSelectSceneController diceSelectSceneController;
    [SerializeField] private Transform trialLayoutRoot;
    [SerializeField] private ProgressionTrialSlotUI trialSlotPrefab;
    [SerializeField] private Slider rankProgressSlider;
    [SerializeField] private TMP_Text rankProgressText;
    [SerializeField] private TMP_Text rankNameText;

    [Header("Rank progress label")]
    [Tooltip("Format for rank progress text. {0} = completed trials, {1} = total trials on this rank.")]
    [SerializeField] private string rankProgressTextFormat = "{0}/{1} Completed";

    readonly List<ProgressionTrialSlotUI> _spawnedSlots = new List<ProgressionTrialSlotUI>();

    void Awake()
    {
        if (diceSelectSceneController == null)
            diceSelectSceneController = FindObjectOfType<DiceSelectSceneController>(true);

        if (trialLayoutRoot == null)
            Debug.LogError("ProgressionTrialsPanelUI: assign trialLayoutRoot (Layout Group parent).", this);
        if (trialSlotPrefab == null)
            Debug.LogError("ProgressionTrialsPanelUI: assign trialSlotPrefab.", this);
    }

    void OnEnable()
    {
        if (diceSelectSceneController != null)
            diceSelectSceneController.CharacterPreviewChanged += OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged += OnProgressionDataChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (diceSelectSceneController != null)
            diceSelectSceneController.CharacterPreviewChanged -= OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged -= OnProgressionDataChanged;
    }

    void OnCharacterPreviewChanged(PlayerDataSO character) => Refresh();

    void OnProgressionDataChanged(PlayerDataSO character)
    {
        if (!TryGetSelectedCharacter(out var selected))
            return;
        if (selected == character)
            Refresh();
    }

    public void Refresh()
    {
        ClearSlots();

        if (!TryGetSelectedCharacter(out var character))
        {
            UpdateRankHeader(null, 0, 0);
            UpdateProgressBar(0, 0);
            return;
        }

        var progression = ProgressionManager.TryGetRuntime();
        if (progression == null)
        {
            if (character.progressionCatalog != null)
                progression = ProgressionManager.EnsureRuntime(character.progressionCatalog);
        }

        if (progression == null)
        {
            Debug.LogError("ProgressionTrialsPanelUI: ProgressionManager missing.", this);
            return;
        }

        if (!progression.IsInitializedFor(character))
            progression.InitializeForCharacter(character);

        var rank = progression.GetActiveRank();
        progression.GetActiveRankTrialCounts(out var completed, out var total);
        UpdateRankHeader(rank, completed, total);
        UpdateProgressBar(completed, total);

        if (rank?.associatedTrials == null || trialSlotPrefab == null || trialLayoutRoot == null)
            return;

        var displayOrder = BuildTrialDisplayOrder(rank.associatedTrials, progression);
        for (var i = 0; i < displayOrder.Count; i++)
        {
            var entry = displayOrder[i];
            var slot = Instantiate(trialSlotPrefab, trialLayoutRoot);
            slot.Bind(entry.trial, entry.state);
            _spawnedSlots.Add(slot);
        }
    }

    /// <summary>Completed trials first (top of layout group), then incomplete; catalog order within each group.</summary>
    static List<(PlayerTrialSO trial, TrialSaveData state)> BuildTrialDisplayOrder(
        IReadOnlyList<PlayerTrialSO> trials,
        ProgressionManager progression)
    {
        var ordered = new List<(PlayerTrialSO trial, TrialSaveData state, int catalogIndex)>();
        for (var i = 0; i < trials.Count; i++)
        {
            var trial = trials[i];
            if (trial == null)
                continue;

            if (!progression.TryGetTrialState(trial.trialID, out var state))
                state = new TrialSaveData { trialID = trial.trialID, currentValue = 0, isCompleted = false };

            ordered.Add((trial, state, i));
        }

        ordered.Sort((a, b) =>
        {
            if (a.state.isCompleted != b.state.isCompleted)
                return a.state.isCompleted ? -1 : 1;

            return a.catalogIndex.CompareTo(b.catalogIndex);
        });

        var result = new List<(PlayerTrialSO trial, TrialSaveData state)>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
            result.Add((ordered[i].trial, ordered[i].state));
        return result;
    }

    void UpdateRankHeader(PlayerRankSO rank, int completed, int total)
    {
        if (rankNameText == null)
            return;

        if (rank == null)
        {
            rankNameText.text = string.Empty;
            return;
        }

        var name = string.IsNullOrWhiteSpace(rank.rankName) ? $"Rank {rank.rankIndex}" : rank.rankName;
        rankNameText.text = name;
    }

    void UpdateProgressBar(int completed, int total)
    {
        if (rankProgressText != null)
            rankProgressText.text = FormatRankProgressText(completed, total);

        if (rankProgressSlider == null)
            return;

        rankProgressSlider.interactable = false;
        if (total <= 0)
        {
            rankProgressSlider.value = 0f;
            rankProgressSlider.minValue = 0f;
            rankProgressSlider.maxValue = 1f;
            return;
        }

        rankProgressSlider.minValue = 0f;
        rankProgressSlider.maxValue = total;
        rankProgressSlider.wholeNumbers = true;
        rankProgressSlider.value = completed;
    }

    string FormatRankProgressText(int completed, int total)
    {
        var format = string.IsNullOrEmpty(rankProgressTextFormat) ? "{0}/{1}" : rankProgressTextFormat;
        try
        {
            return string.Format(format, completed, total);
        }
        catch (System.FormatException ex)
        {
            Debug.LogWarning($"ProgressionTrialsPanelUI: invalid rankProgressTextFormat '{format}': {ex.Message}. Using default.", this);
            return $"{completed}/{total}";
        }
    }

    void ClearSlots()
    {
        for (var i = 0; i < _spawnedSlots.Count; i++)
        {
            if (_spawnedSlots[i] != null)
                Destroy(_spawnedSlots[i].gameObject);
        }

        _spawnedSlots.Clear();
    }

    bool TryGetSelectedCharacter(out PlayerDataSO character)
    {
        character = null;
        if (diceSelectSceneController != null && diceSelectSceneController.TryGetPreviewCharacter(out character))
            return character != null;

        var container = PlayerDataContainer.Instance;
        if (container?.ActiveCharacterTemplate != null)
        {
            character = container.ActiveCharacterTemplate;
            return true;
        }

        return false;
    }
}
