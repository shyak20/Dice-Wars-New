using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// On Dice Select, shows one popup per unacknowledged completed trial for the previewed character,
/// then a level-up popup when all trials on the rank were finished.
/// Enables <see cref="progressionCelebrationRoot"/> only while a popup is visible; disables it when done.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class DiceSelectProgressionCelebrationController : MonoBehaviour
{
    [SerializeField] private DiceSelectSceneController diceSelectSceneController;
    [SerializeField] private ProgressionTrialCompletedPopupView trialCompletedPopup;
    [SerializeField] private ProgressionRankUpPopupView rankUpPopup;
    [Tooltip("Parent of celebration popups (and optional overlay art). Disabled whenever no popup is showing.")]
    [SerializeField] private GameObject progressionCelebrationRoot;
    [Tooltip("Optional full-screen blocker under celebration root.")]
    [SerializeField] private GameObject inputBlocker;

    readonly List<PlayerTrialSO> _pendingTrials = new List<PlayerTrialSO>();
    Coroutine _flowCoroutine;
    bool _flowRunning;

    public bool IsFlowRunning => _flowRunning;

    void Awake()
    {
        if (diceSelectSceneController == null)
            diceSelectSceneController = FindObjectOfType<DiceSelectSceneController>(true);
        if (trialCompletedPopup == null)
            Debug.LogError("DiceSelectProgressionCelebrationController: assign trialCompletedPopup.", this);
        if (rankUpPopup == null)
            Debug.LogError("DiceSelectProgressionCelebrationController: assign rankUpPopup.", this);
        if (progressionCelebrationRoot == null)
            Debug.LogError("DiceSelectProgressionCelebrationController: assign progressionCelebrationRoot.", this);

        if (inputBlocker == null && progressionCelebrationRoot != null)
        {
            var blocker = progressionCelebrationRoot.transform.Find("Input Blocker");
            if (blocker != null)
                inputBlocker = blocker.gameObject;
        }

        SetCelebrationRootActive(false);
    }

    void OnEnable()
    {
        if (diceSelectSceneController != null)
            diceSelectSceneController.CharacterPreviewChanged += OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged += OnProgressionDataChanged;
    }

    void Start() => TryStartCelebrationFlow();

    void OnDisable()
    {
        if (diceSelectSceneController != null)
            diceSelectSceneController.CharacterPreviewChanged -= OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged -= OnProgressionDataChanged;
        StopFlow();
    }

    void OnCharacterPreviewChanged(PlayerDataSO character) => TryStartCelebrationFlow();

    void OnProgressionDataChanged(PlayerDataSO character)
    {
        if (diceSelectSceneController == null || !diceSelectSceneController.TryGetPreviewCharacter(out var selected))
            return;

        if (selected == character)
            TryStartCelebrationFlow();
    }

    void TryStartCelebrationFlow()
    {
        if (_flowRunning)
            return;

        StopFlow();

        if (!diceSelectSceneController.TryGetPreviewCharacter(out var character))
            return;

        var progression = ResolveProgression(character);
        if (progression == null || !progression.HasPendingCelebrations())
            return;

        DiceSelectProgressionDisplayGate.SetDeferred(true);
        diceSelectSceneController.SetInteractionBlocked(true);
        _flowCoroutine = StartCoroutine(RunCelebrationFlow(character, progression));
    }

    IEnumerator RunCelebrationFlow(PlayerDataSO character, ProgressionManager progression)
    {
        _flowRunning = true;

        try
        {
            progression.CollectUnacknowledgedTrials(_pendingTrials);
            for (var i = 0; i < _pendingTrials.Count; i++)
            {
                var trial = _pendingTrials[i];
                if (trial == null)
                    continue;

                var acknowledged = false;
                SetCelebrationRootActive(true);
                trialCompletedPopup.Show(trial, () => acknowledged = true);
                while (!acknowledged)
                    yield return null;

                trialCompletedPopup.Hide();
                SetCelebrationRootActive(false);
                progression.AcknowledgeTrialCelebration(trial.trialID);

                if (!progression.IsInitializedFor(character))
                    break;
            }

            _pendingTrials.Clear();

            if (progression.HasPendingRankUpCelebration())
            {
                var rankToCelebrate = progression.GetActiveRank();
                if (rankToCelebrate != null)
                {
                    var rankAcknowledged = false;
                    SetCelebrationRootActive(true);
                    rankUpPopup.Show(rankToCelebrate, () => rankAcknowledged = true);
                    while (!rankAcknowledged)
                        yield return null;

                    rankUpPopup.Hide();
                    SetCelebrationRootActive(false);
                }

                progression.AcknowledgeRankUpCelebration();
            }
        }
        finally
        {
            diceSelectSceneController.SetInteractionBlocked(false);
            DiceSelectProgressionDisplayGate.SetDeferred(false);
            EndCelebrationFlow();
        }
    }

    void StopFlow()
    {
        if (_flowCoroutine != null)
        {
            StopCoroutine(_flowCoroutine);
            _flowCoroutine = null;
        }

        EndCelebrationFlow();
    }

    void EndCelebrationFlow()
    {
        _flowRunning = false;
        _flowCoroutine = null;
        _pendingTrials.Clear();
        trialCompletedPopup?.Hide();
        rankUpPopup?.Hide();
        SetCelebrationRootActive(false);

        if (DiceSelectProgressionDisplayGate.IsDeferred)
        {
            diceSelectSceneController.SetInteractionBlocked(false);
            DiceSelectProgressionDisplayGate.SetDeferred(false);
        }
    }

    void SetCelebrationRootActive(bool active)
    {
        if (progressionCelebrationRoot != null)
            progressionCelebrationRoot.SetActive(active);

        if (inputBlocker != null)
            inputBlocker.SetActive(active);
    }

    static ProgressionManager ResolveProgression(PlayerDataSO character)
    {
        if (character == null)
            return null;

        var progression = ProgressionManager.TryGetRuntime();
        if (progression == null && character.progressionCatalog != null)
            progression = ProgressionManager.EnsureRuntime(character.progressionCatalog);

        if (progression == null)
            return null;

        if (!progression.IsInitializedFor(character))
            progression.InitializeForCharacter(character);

        return progression;
    }
}
