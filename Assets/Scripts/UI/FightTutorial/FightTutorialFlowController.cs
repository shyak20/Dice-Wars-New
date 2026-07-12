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
    bool _flowFinished;
    readonly List<Button> _boundAdvanceButtons = new List<Button>();
    bool _createdOverlayCanvas;
    Coroutine _showPhaseDelayRoutine;

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

        // Session may have initialized before this object enabled (additive fight / late tutorial root).
        if (!_combatSessionReady)
            _combatSessionReady = IsCombatSessionLikelyReady();

        if (_flowFinished)
            return;

        // Root intentionally off (e.g. FirstEncounterDayVisibility already consumed) — do not drive children.
        if (rootTutorial != null && !rootTutorial.activeSelf)
        {
            _flowFinished = true;
            return;
        }

        if (_phaseIndex < 0)
            TryBeginOrResumeFlow();
        else if (_waitingToActivate && IsTriggerAlreadySatisfied(_pendingActivateTrigger))
            ShowPhase(phases[_phaseIndex]);
        else if (!_waitingToActivate)
            // Fight scene preload disables roots after the first OnEnable; re-apply blocker / objects / button.
            RefreshActivePhasePresentation();
    }

    bool IsCombatSessionLikelyReady()
    {
        return combatManager != null && combatManager.player != null;
    }

    void OnDisable()
    {
        CombatEvents.OnCombatSessionInitialized -= HandleCombatSessionInitialized;
        CombatEvents.OnDieToggled -= HandleDieToggled;
        CombatEvents.OnRollCommand -= HandleRollCommand;
        CombatEvents.OnRollResultsResolved -= HandleRollResultsResolved;
        CombatEvents.OnVictoryScreenAppeared -= HandleVictoryScreenAppeared;

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

        ArmActivateForIndex(0);
    }

    void ArmActivateForIndex(int index)
    {
        if (index < 0 || index >= phases.Count)
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

        SetPhaseObjectsActive(phase, true);
        ApplyPhaseSorting(phase);
        SetBlocker(phase.enableInteractionBlocker);
        BindAdvanceButton(phase);
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

        var next = _phaseIndex + 1;
        _phaseIndex = -1;
        if (next >= phases.Count)
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
            FightTutorialTrigger.Immediate => true,
            _ => false
        };
    }

    void HandleCombatSessionInitialized()
    {
        _combatSessionReady = true;
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
        OnCombatTrigger(FightTutorialTrigger.VictoryScreenAppear);
    }

    void BindAdvanceButton(FightTutorialPhase phase)
    {
        UnbindAdvanceButton();

        if (phase == null || !phase.HasAdvanceButtons())
            return;

        if (phase.completeWhen != FightTutorialTrigger.AdvanceButton && !phase.allowButtonSkip)
            return;

        for (var i = 0; i < phase.advanceButtons.Count; i++)
        {
            var button = phase.advanceButtons[i];
            if (button == null)
                continue;

            button.interactable = true;
            button.onClick.RemoveListener(OnAdvanceClicked);
            button.onClick.AddListener(OnAdvanceClicked);
            _boundAdvanceButtons.Add(button);
        }
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
