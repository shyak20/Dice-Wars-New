using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fight-scene tutorial director: ordered phases that can appear / finish on combat triggers
/// (select die, first roll, …) or an advance button. Sorting targets are lifted onto a Screen Space Overlay.
/// </summary>
public sealed class FightTutorialFlowController : MonoBehaviour
{
    [Tooltip("Optional. Used to detect combat already started if this object enables after OnCombatSessionInitialized.")]
    [SerializeField] private CombatManager combatManager;

    [Tooltip("Used to resolve Dice Tray Index sorting targets to runtime-spawned die buttons.")]
    [SerializeField] private CombatUIController combatUIController;

    [Tooltip("Used to resolve Face Action Option advance buttons when Use Face Select Options As Advance Buttons is on.")]
    [SerializeField] private FacePickerView facePickerView;

    [Tooltip("Used to resolve win-stage RunRewardOfferRow action buttons for Victory Screen Appear phases.")]
    [SerializeField] private WinStageFlowController winStageFlowController;

    [Tooltip("Whole tutorial root — disabled when the last phase completes.")]
    [SerializeField] private GameObject rootTutorial;

    [Tooltip("Optional full-screen blocker (dim Image). Enabled only while the active phase has Enable Interaction Blocker.")]
    [SerializeField] private GameObject interactionBlocker;

    [Tooltip("Optional. Screen Space Overlay used to lift phase sorting targets above all Camera canvases. Created at runtime if unset.")]
    [SerializeField] private Canvas tutorialOverlayCanvas;

    [Tooltip("Sorting order for the runtime Overlay canvas when tutorialOverlayCanvas is not assigned.")]
    [SerializeField] private int tutorialOverlaySortingOrder = 32000;

    [SerializeField] private List<FightTutorialPhase> phases = new List<FightTutorialPhase>();

    readonly FightTutorialSortingSession _sorting = new FightTutorialSortingSession();

    int _phaseIndex = -1;
    bool _waitingToActivate;
    FightTutorialTrigger _pendingActivateTrigger;
    bool _combatSessionReady;
    bool _sawFirstRoll;
    bool _victoryScreenAppeared;
    bool _faceSelectAppeared;
    bool _awaitingElementValueDrag;
    bool _flowFinished;
    readonly List<Button> _boundAdvanceButtons = new List<Button>();
    bool _createdOverlayCanvas;
    Coroutine _showPhaseDelayRoutine;
    CanvasGroup _phaseRootPassthroughGroup;
    bool _phaseRootPassthroughAdded;
    bool _phaseRootPassthroughPrevBlocks;
    bool _phaseRootPassthroughPrevIgnore;

    void Awake()
    {
        if (rootTutorial == null)
            rootTutorial = gameObject;

        EnsureTutorialCanvasRaycaster();

        for (var i = 0; i < phases.Count; i++)
            phases[i]?.Validate(nameof(FightTutorialFlowController) + $" on '{name}'", i);

        for (var i = 0; i < phases.Count; i++)
        {
            var phase = phases[i];
            if (phase == null)
                continue;

            if (phase.phaseRoot != null && phase.phaseRoot != rootTutorial)
                phase.phaseRoot.SetActive(false);

            SetPhaseObjectsActive(phase, false);
        }

        SetBlocker(false);
    }

    void OnEnable()
    {
        CombatEvents.OnCombatSessionInitialized += HandleCombatSessionInitialized;
        CombatEvents.OnDieToggled += HandleDieToggled;
        CombatEvents.OnRollCommand += HandleRollCommand;
        CombatEvents.OnRollResultsResolved += HandleRollResultsResolved;
        CombatEvents.OnVictoryScreenAppeared += HandleVictoryScreenAppeared;
        CombatEvents.OnFaceSelectAppeared += HandleFaceSelectAppeared;
        CombatEvents.OnTargetAssignmentModeChanged += HandleTargetAssignmentModeChanged;
        CombatEvents.OnElementValueDroppedOnEnemy += HandleElementValueDroppedOnEnemy;

        // Session may have initialized before this object enabled (additive fight / late tutorial root).
        if (!_combatSessionReady)
            _combatSessionReady = IsCombatSessionLikelyReady();

        if (_flowFinished)
            return;

        SyncElementValueDragStateFromCombat();

        // FirstEncounter may leave rootTutorial inactive; do not kill the flow — ShowPhase re-enables it.
        // Already-seen phases are skipped via FightTutorialPhaseProgress until progression reset.
        if (_phaseIndex < 0)
            TryBeginOrResumeFlow();
        else if (_waitingToActivate && IsTriggerAlreadySatisfied(_pendingActivateTrigger))
            ShowPhase(phases[_phaseIndex]);
        else if (!_waitingToActivate)
            RefreshActivePhasePresentation();
    }

    bool IsCombatSessionLikelyReady()
    {
        return combatManager != null && combatManager.player != null;
    }

    void SyncElementValueDragStateFromCombat()
    {
        var waiting = IsCombatWaitingForElementValueDrag();
        if (waiting == _awaitingElementValueDrag)
            return;

        _awaitingElementValueDrag = waiting;
        if (waiting)
            OnCombatTrigger(FightTutorialTrigger.AwaitElementValueDrag);
    }

    bool IsCombatWaitingForElementValueDrag()
    {
        var combat = combatManager != null ? combatManager : FindObjectOfType<CombatManager>();
        if (combat == null)
            return false;

        var assignment = combat.TargetAssignment;
        return assignment != null && assignment.IsWaitingForPlayerAssignment;
    }

    void OnDisable()
    {
        CombatEvents.OnCombatSessionInitialized -= HandleCombatSessionInitialized;
        CombatEvents.OnDieToggled -= HandleDieToggled;
        CombatEvents.OnRollCommand -= HandleRollCommand;
        CombatEvents.OnRollResultsResolved -= HandleRollResultsResolved;
        CombatEvents.OnVictoryScreenAppeared -= HandleVictoryScreenAppeared;
        CombatEvents.OnFaceSelectAppeared -= HandleFaceSelectAppeared;
        CombatEvents.OnTargetAssignmentModeChanged -= HandleTargetAssignmentModeChanged;
        CombatEvents.OnElementValueDroppedOnEnemy -= HandleElementValueDroppedOnEnemy;

        TearDownActivePhasePresentation();
    }

    void TryBeginOrResumeFlow()
    {
        if (_flowFinished || phases == null || phases.Count == 0)
        {
            EndFlow();
            return;
        }

        if (_phaseIndex >= 0)
            return;

        ArmActivateForIndex(FindNextUnseenPhaseIndex(0));
    }

    /// <summary>Next phase that has not been shown yet (once-ever prefs). -1 if none remain.</summary>
    int FindNextUnseenPhaseIndex(int fromInclusive)
    {
        if (phases == null)
            return -1;

        for (var i = Mathf.Max(0, fromInclusive); i < phases.Count; i++)
        {
            var phase = phases[i];
            if (phase == null || phase.phaseRoot == null)
                continue;
            if (IsPhaseSeen(phase, i))
                continue;
            return i;
        }

        return -1;
    }

    bool IsPhaseSeen(FightTutorialPhase phase, int index)
    {
        return FightTutorialPhaseProgress.IsSeen(phase.ResolvePersistenceId(index));
    }

    void MarkPhaseSeen(FightTutorialPhase phase, int index)
    {
        if (phase == null)
            return;
        FightTutorialPhaseProgress.MarkSeen(phase.ResolvePersistenceId(index));
    }

    void ArmActivateForIndex(int index)
    {
        index = FindNextUnseenPhaseIndex(index);
        if (index < 0)
        {
            EndFlow();
            return;
        }

        var phase = phases[index];
        if (phase == null || phase.phaseRoot == null)
        {
            Debug.LogError($"{nameof(FightTutorialFlowController)} on '{name}': phases[{index}] is invalid.", this);
            EndFlow();
            return;
        }

        _phaseIndex = index;
        var trigger = phase.activateWhen;

        if (trigger == FightTutorialTrigger.AwaitElementValueDrag)
            _awaitingElementValueDrag = IsCombatWaitingForElementValueDrag();

        if (trigger == FightTutorialTrigger.Immediate || IsTriggerAlreadySatisfied(trigger))
        {
            _waitingToActivate = false;
            ShowPhase(phase);
            return;
        }

        _waitingToActivate = true;
        _pendingActivateTrigger = trigger;
        TearDownActivePhasePresentation();
        if (phase.phaseRoot != null)
            phase.phaseRoot.SetActive(false);
    }

    void ShowPhase(FightTutorialPhase phase)
    {
        if (phase == null)
            return;

        StopShowPhaseDelay();
        _waitingToActivate = false;
        UnbindAdvanceButton();
        _sorting.Restore();

        if (rootTutorial != null && !rootTutorial.activeSelf)
            rootTutorial.SetActive(true);

        EnsureTutorialCanvasRaycaster();

        if (phase.phaseRoot != null)
            phase.phaseRoot.SetActive(false);
        SetPhaseObjectsActive(phase, false);
        SetBlocker(false);

        var delay = Mathf.Max(0f, phase.phaseRootEnableDelaySeconds);
        if (delay <= 0f)
        {
            PresentPhase(phase);
            return;
        }

        _showPhaseDelayRoutine = StartCoroutine(CoShowPhaseAfterDelay(phase, delay, _phaseIndex));
    }

    IEnumerator CoShowPhaseAfterDelay(FightTutorialPhase phase, float delaySeconds, int phaseIndex)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        _showPhaseDelayRoutine = null;
        if (_flowFinished || !isActiveAndEnabled || _phaseIndex != phaseIndex || phase == null)
            yield break;

        PresentPhase(phase);
    }

    void PresentPhase(FightTutorialPhase phase)
    {
        if (phase.phaseRoot != null)
            phase.phaseRoot.SetActive(true);

        // Mark when first shown so later fights skip this tip (until progression reset).
        MarkPhaseSeen(phase, _phaseIndex);

        SetPhaseObjectsActive(phase, true);
        ApplyPhaseSorting(phase);
        SetBlocker(phase.enableInteractionBlocker);
        ApplyPhaseRootRaycastPassthrough(phase);
        BindAdvanceButton(phase);
    }

    /// <summary>
    /// When the interaction blocker is off and this phase has no advance buttons, tip UI must not
    /// steal raycasts from fight UI (e.g. full-screen invisible Buttons over EnemyDropTarget).
    /// </summary>
    void ApplyPhaseRootRaycastPassthrough(FightTutorialPhase phase)
    {
        ClearPhaseRootRaycastPassthrough();

        if (phase == null || phase.phaseRoot == null)
            return;

        if (phase.enableInteractionBlocker || phase.HasAdvanceButtons())
            return;

        var group = phase.phaseRoot.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = phase.phaseRoot.AddComponent<CanvasGroup>();
            _phaseRootPassthroughAdded = true;
        }
        else
        {
            _phaseRootPassthroughPrevBlocks = group.blocksRaycasts;
            _phaseRootPassthroughPrevIgnore = group.ignoreParentGroups;
            _phaseRootPassthroughAdded = false;
        }

        group.blocksRaycasts = false;
        _phaseRootPassthroughGroup = group;
    }

    void ClearPhaseRootRaycastPassthrough()
    {
        if (_phaseRootPassthroughGroup == null)
            return;

        if (_phaseRootPassthroughAdded)
        {
            if (Application.isPlaying)
                Destroy(_phaseRootPassthroughGroup);
            else
                DestroyImmediate(_phaseRootPassthroughGroup);
        }
        else
        {
            _phaseRootPassthroughGroup.blocksRaycasts = _phaseRootPassthroughPrevBlocks;
            _phaseRootPassthroughGroup.ignoreParentGroups = _phaseRootPassthroughPrevIgnore;
        }

        _phaseRootPassthroughGroup = null;
        _phaseRootPassthroughAdded = false;
    }

    void StopShowPhaseDelay()
    {
        if (_showPhaseDelayRoutine != null)
        {
            StopCoroutine(_showPhaseDelayRoutine);
            _showPhaseDelayRoutine = null;
        }
    }

    void ApplyPhaseSorting(FightTutorialPhase phase)
    {
        if (phase?.sortingTargets == null || phase.sortingTargets.Count == 0)
            return;

        if (NeedsDiceTrayResolution(phase.sortingTargets))
        {
            if (!EnsureCombatUIController())
                return;

            combatUIController.EnsureDiceButtonsReady();
        }

        _sorting.Apply(phase.sortingTargets, EnsureTutorialOverlayCanvas(), ResolveSortingTarget);
    }

    bool EnsureCombatUIController()
    {
        if (combatUIController != null)
            return true;

        combatUIController = FindObjectOfType<CombatUIController>();
        if (combatUIController != null)
            return true;

        Debug.LogError(
            $"{nameof(FightTutorialFlowController)} on '{name}': no {nameof(CombatUIController)} in the scene — cannot resolve Dice Tray Index sorting targets.",
            this);
        return false;
    }

    static bool NeedsDiceTrayResolution(IReadOnlyList<FightTutorialSortingTarget> targets)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null && targets[i].kind == FightTutorialSortingTargetKind.DiceTrayIndex)
                return true;
        }

        return false;
    }

    GameObject ResolveSortingTarget(FightTutorialSortingTarget spec)
    {
        if (spec == null)
            return null;

        switch (spec.kind)
        {
            case FightTutorialSortingTargetKind.ExplicitGameObject:
                return spec.target;
            case FightTutorialSortingTargetKind.DiceTrayIndex:
                if (combatUIController == null)
                    return null;
                return combatUIController.TryGetTrayDieButtonAt(spec.diceTrayIndex, out var button)
                    ? button
                    : null;
            default:
                return null;
        }
    }

    /// <summary>Re-applies the current phase after this object was disabled (additive fight preload).</summary>
    void RefreshActivePhasePresentation()
    {
        if (_phaseIndex < 0 || _phaseIndex >= phases.Count)
            return;

        ShowPhase(phases[_phaseIndex]);
    }

    void TearDownActivePhasePresentation()
    {
        StopShowPhaseDelay();
        UnbindAdvanceButton();
        ClearPhaseRootRaycastPassthrough();
        _sorting.Restore();

        if (_phaseIndex >= 0 && _phaseIndex < phases.Count)
            SetPhaseObjectsActive(phases[_phaseIndex], false);

        SetBlocker(false);
    }

    void CompleteActivePhase()
    {
        if (_flowFinished || _phaseIndex < 0 || _phaseIndex >= phases.Count)
            return;

        var phase = phases[_phaseIndex];
        TearDownActivePhasePresentation();

        if (phase?.phaseRoot != null)
            phase.phaseRoot.SetActive(false);

        var next = FindNextUnseenPhaseIndex(_phaseIndex + 1);
        _phaseIndex = -1;
        if (next < 0)
        {
            EndFlow();
            return;
        }

        ArmActivateForIndex(next);
    }

    void EndFlow()
    {
        TearDownActivePhasePresentation();

        if (_phaseIndex >= 0 && _phaseIndex < phases.Count && phases[_phaseIndex]?.phaseRoot != null)
            phases[_phaseIndex].phaseRoot.SetActive(false);

        _flowFinished = true;
        _waitingToActivate = false;
        _phaseIndex = -1;

        if (rootTutorial != null)
            rootTutorial.SetActive(false);
    }

    void OnCombatTrigger(FightTutorialTrigger trigger)
    {
        if (_flowFinished || !isActiveAndEnabled)
            return;

        if (_waitingToActivate && trigger == _pendingActivateTrigger)
        {
            var phase = phases[_phaseIndex];
            if (phase != null)
                ShowPhase(phase);
            return;
        }

        if (_waitingToActivate || _phaseIndex < 0 || _phaseIndex >= phases.Count)
            return;

        var active = phases[_phaseIndex];
        if (active == null)
            return;

        if (active.completeWhen == trigger)
            CompleteActivePhase();
    }

    bool IsTriggerAlreadySatisfied(FightTutorialTrigger trigger)
    {
        return trigger switch
        {
            FightTutorialTrigger.CombatSessionReady => _combatSessionReady,
            FightTutorialTrigger.PlayerFirstRoll => _sawFirstRoll,
            FightTutorialTrigger.VictoryScreenAppear => _victoryScreenAppeared,
            FightTutorialTrigger.FaceSelectAppear => _faceSelectAppeared,
            FightTutorialTrigger.AwaitElementValueDrag => _awaitingElementValueDrag,
            FightTutorialTrigger.Immediate => true,
            _ => false
        };
    }

    void HandleCombatSessionInitialized()
    {
        _combatSessionReady = true;
        // Per-fight flags — a prior fight in the same loaded scene must not auto-satisfy late tips.
        _sawFirstRoll = false;
        _victoryScreenAppeared = false;
        _faceSelectAppeared = false;
        SyncElementValueDragStateFromCombat();

        // Previous fight may have EndFlow'd while unseen late tips remain (e.g. Victory not yet shown).
        if (_flowFinished && FindNextUnseenPhaseIndex(0) >= 0)
        {
            _flowFinished = false;
            _phaseIndex = -1;
            _waitingToActivate = false;
            TryBeginOrResumeFlow();
        }

        OnCombatTrigger(FightTutorialTrigger.CombatSessionReady);
    }

    void HandleDieToggled(DieAssetSO _)
    {
        OnCombatTrigger(FightTutorialTrigger.PlayerSelectDie);
    }

    void HandleRollCommand()
    {
        if (!_sawFirstRoll)
        {
            _sawFirstRoll = true;
            OnCombatTrigger(FightTutorialTrigger.PlayerFirstRoll);
        }

        OnCombatTrigger(FightTutorialTrigger.RollButton);
    }

    void HandleRollResultsResolved()
    {
        OnCombatTrigger(FightTutorialTrigger.RollResultsResolved);
    }

    void HandleVictoryScreenAppeared()
    {
        _victoryScreenAppeared = true;

        // Multi-enemy tip is combat-only; if we never got a drag gate, skip it so victory tips can run.
        if (_waitingToActivate && _pendingActivateTrigger == FightTutorialTrigger.AwaitElementValueDrag)
            SkipWaitingPhaseWithoutShowing();

        OnCombatTrigger(FightTutorialTrigger.VictoryScreenAppear);
    }

    void SkipWaitingPhaseWithoutShowing()
    {
        if (!_waitingToActivate || _phaseIndex < 0)
            return;

        StopShowPhaseDelay();
        var next = FindNextUnseenPhaseIndex(_phaseIndex + 1);
        _waitingToActivate = false;
        _phaseIndex = -1;
        ArmActivateForIndex(next);
    }

    void HandleFaceSelectAppeared()
    {
        _faceSelectAppeared = true;
        OnCombatTrigger(FightTutorialTrigger.FaceSelectAppear);
    }

    void HandleTargetAssignmentModeChanged(bool waitingForDrag)
    {
        _awaitingElementValueDrag = waitingForDrag;
        if (waitingForDrag)
            OnCombatTrigger(FightTutorialTrigger.AwaitElementValueDrag);
    }

    void HandleElementValueDroppedOnEnemy()
    {
        OnCombatTrigger(FightTutorialTrigger.ElementValueDroppedOnEnemy);
    }

    void BindAdvanceButton(FightTutorialPhase phase)
    {
        UnbindAdvanceButton();

        if (phase == null)
            return;

        if (phase.completeWhen != FightTutorialTrigger.AdvanceButton && !phase.allowButtonSkip)
            return;

        if (phase.advanceButtons != null)
        {
            for (var i = 0; i < phase.advanceButtons.Count; i++)
            {
                var button = phase.advanceButtons[i];
                if (button == null)
                    continue;
                BindOneAdvanceButton(button);
            }
        }

        if (phase.useFaceSelectOptionsAsAdvanceButtons ||
            phase.activateWhen == FightTutorialTrigger.FaceSelectAppear)
            BindFaceSelectOptionAdvanceButtons();

        if (phase.useVictoryRewardRowsAsAdvanceButtons ||
            phase.activateWhen == FightTutorialTrigger.VictoryScreenAppear)
            BindVictoryRewardRowAdvanceButtons();
    }

    void BindFaceSelectOptionAdvanceButtons()
    {
        var picker = EnsureFacePickerView();
        if (picker == null)
        {
            Debug.LogError(
                $"{nameof(FightTutorialFlowController)} on '{name}': assign facePickerView (or ensure a FacePickerView is in the scene) for Face Select Option advance buttons.",
                this);
            return;
        }

        var scratch = new List<Button>();
        picker.CollectOptionButtons(scratch);
        if (scratch.Count == 0)
        {
            Debug.LogError(
                $"{nameof(FightTutorialFlowController)} on '{name}': Face Select Option buttons were empty — open the face picker before this phase presents.",
                this);
            return;
        }

        for (var i = 0; i < scratch.Count; i++)
            BindOneAdvanceButton(scratch[i]);
    }

    void BindVictoryRewardRowAdvanceButtons()
    {
        var winStage = EnsureWinStageFlowController();
        if (winStage == null)
        {
            Debug.LogError(
                $"{nameof(FightTutorialFlowController)} on '{name}': assign winStageFlowController (or ensure a WinStageFlowController is in the scene) for victory reward row advance buttons.",
                this);
            return;
        }

        var scratch = new List<Button>();
        winStage.CollectRewardOfferActionButtons(scratch);
        if (scratch.Count == 0)
        {
            Debug.LogError(
                $"{nameof(FightTutorialFlowController)} on '{name}': no RunRewardOfferRow action buttons found — victory rewards must be built before this phase presents.",
                this);
            return;
        }

        for (var i = 0; i < scratch.Count; i++)
            BindOneAdvanceButton(scratch[i]);
    }

    void BindOneAdvanceButton(Button button)
    {
        if (button == null)
            return;

        button.interactable = true;
        button.onClick.RemoveListener(OnAdvanceClicked);
        button.onClick.AddListener(OnAdvanceClicked);
        _boundAdvanceButtons.Add(button);
    }

    FacePickerView EnsureFacePickerView()
    {
        if (facePickerView != null)
            return facePickerView;

        facePickerView = FindObjectOfType<FacePickerView>(true);
        return facePickerView;
    }

    WinStageFlowController EnsureWinStageFlowController()
    {
        if (winStageFlowController != null)
            return winStageFlowController;

        winStageFlowController = FindObjectOfType<WinStageFlowController>(true);
        return winStageFlowController;
    }

    void UnbindAdvanceButton()
    {
        for (var i = 0; i < _boundAdvanceButtons.Count; i++)
        {
            var button = _boundAdvanceButtons[i];
            if (button != null)
                button.onClick.RemoveListener(OnAdvanceClicked);
        }

        _boundAdvanceButtons.Clear();
    }

    void OnAdvanceClicked()
    {
        if (_flowFinished || _waitingToActivate || _phaseIndex < 0 || !isActiveAndEnabled)
            return;

        var phase = phases[_phaseIndex];
        if (phase == null)
            return;

        if (phase.completeWhen != FightTutorialTrigger.AdvanceButton && !phase.allowButtonSkip)
            return;

        CompleteActivePhase();
    }

    void SetBlocker(bool visible)
    {
        if (interactionBlocker != null)
            interactionBlocker.SetActive(visible);
    }

    void EnsureTutorialCanvasRaycaster()
    {
        var canvasHost = rootTutorial != null ? rootTutorial : gameObject;
        var canvas = canvasHost.GetComponent<Canvas>();
        if (canvas == null)
            return;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    Canvas EnsureTutorialOverlayCanvas()
    {
        if (tutorialOverlayCanvas != null)
        {
            ConfigureOverlayCanvas(tutorialOverlayCanvas, tutorialOverlaySortingOrder);
            return tutorialOverlayCanvas;
        }

        var go = new GameObject("Tutorial Highlight Overlay", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var canvas = go.AddComponent<Canvas>();
        ConfigureOverlayCanvas(canvas, tutorialOverlaySortingOrder);
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        tutorialOverlayCanvas = canvas;
        _createdOverlayCanvas = true;
        return canvas;
    }

    static void ConfigureOverlayCanvas(Canvas canvas, int sortingOrder)
    {
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    void OnDestroy()
    {
        _sorting.Restore();
        if (_createdOverlayCanvas && tutorialOverlayCanvas != null)
        {
            if (Application.isPlaying)
                Destroy(tutorialOverlayCanvas.gameObject);
            else
                DestroyImmediate(tutorialOverlayCanvas.gameObject);
            tutorialOverlayCanvas = null;
        }
    }

    static void SetPhaseObjectsActive(FightTutorialPhase phase, bool active)
    {
        if (phase?.objectsToEnable == null)
            return;

        for (var i = 0; i < phase.objectsToEnable.Count; i++)
        {
            var go = phase.objectsToEnable[i];
            if (go != null)
                go.SetActive(active);
        }
    }
}
