using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// On Dice Select, shows one popup per unacknowledged completed trial for the previewed character,
/// then a level-up popup when all trials on the rank were finished.
/// </summary>
public sealed class DiceSelectProgressionCelebrationController : MonoBehaviour
{
    [SerializeField] private DiceSelectSceneController diceSelectSceneController;
    [SerializeField] private ProgressionTrialCompletedPopupView trialCompletedPopup;
    [SerializeField] private ProgressionRankUpPopupView rankUpPopup;
    [Tooltip("Root object for trial/rank-up popups (hidden after rank-up is acknowledged).")]
    [SerializeField] private GameObject progressionCelebrationRoot;
    [Tooltip("Full-screen raycast blocker; active only while a trial-complete or rank-up popup is open.")]
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
    }

    void OnEnable()
    {
        if (diceSelectSceneController != null)
            diceSelectSceneController.CharacterPreviewChanged += OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged += OnProgressionDataChanged;
    }

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
        StopFlow();

        if (!diceSelectSceneController.TryGetPreviewCharacter(out var character))
            return;

        var progression = ResolveProgression(character);
        if (progression == null || !progression.HasPendingCelebrations())
            return;

        ShowCelebrationRoot();
        _flowCoroutine = StartCoroutine(RunCelebrationFlow(character, progression));
    }

    IEnumerator RunCelebrationFlow(PlayerDataSO character, ProgressionManager progression)
    {
        _flowRunning = true;
        var completedRankUpSequence = false;

        progression.CollectUnacknowledgedTrials(_pendingTrials);
        for (var i = 0; i < _pendingTrials.Count; i++)
        {
            var trial = _pendingTrials[i];
            if (trial == null)
                continue;

            var acknowledged = false;
            SetPopupBlocking(true);
            trialCompletedPopup.Show(trial, () => acknowledged = true);
            while (!acknowledged)
                yield return null;
            SetPopupBlocking(false);

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
                SetPopupBlocking(true);
                rankUpPopup.Show(rankToCelebrate, () => rankAcknowledged = true);
                while (!rankAcknowledged)
                    yield return null;
                SetPopupBlocking(false);
            }

            progression.AcknowledgeRankUpCelebration();
            completedRankUpSequence = true;
        }

        if (completedRankUpSequence)
            HideCelebrationRoot();
        _flowRunning = false;
        _flowCoroutine = null;

        diceSelectSceneController.RefreshCharacterDisplayPublic();
    }

    void StopFlow()
    {
        if (_flowCoroutine != null)
        {
            StopCoroutine(_flowCoroutine);
            _flowCoroutine = null;
        }

        _flowRunning = false;
        _pendingTrials.Clear();
        trialCompletedPopup?.Hide();
        rankUpPopup?.Hide();
        SetPopupBlocking(false);
    }

    void SetPopupBlocking(bool active)
    {
        if (inputBlocker != null)
            inputBlocker.SetActive(active);

        diceSelectSceneController?.SetInteractionBlocked(active);
    }

    void ShowCelebrationRoot()
    {
        if (progressionCelebrationRoot != null)
            progressionCelebrationRoot.SetActive(true);
    }

    void HideCelebrationRoot()
    {
        if (progressionCelebrationRoot != null)
            progressionCelebrationRoot.SetActive(false);
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
