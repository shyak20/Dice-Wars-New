using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Map-only panel for <see cref="MapEventType.Unknown"/> tiles.
/// Events with <see cref="UnknownMapEventSO.triggersCombat"/> skip the panel and start a fight immediately; otherwise the player dismisses via option rows built from <see cref="optionRowPrefab"/>.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class MapUnknownEventPanel : MonoBehaviour, IUnknownMapEventOutcomeHost
{
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI titleTextMesh;
    [SerializeField] private TextMeshProUGUI descriptionTextMesh;
    [Tooltip("Shows UnknownMapEventSO.eventArt when the event has art assigned.")]
    [SerializeField] private Image eventArtImage;
    [Tooltip("Vertical, Horizontal, or Grid Layout Group whose RectTransform parents all choice row instances.")]
    [SerializeField] private LayoutGroup optionChoicesLayoutGroup;
    [Tooltip("Prefab root must include UnknownMapEventChoiceRowView (Button + TMP label). One instance per visible choice.")]
    [SerializeField] private GameObject optionRowPrefab;
    [SerializeField] private MapUnknownEventDieChoicePopupView dieChoicePopup;
    [Tooltip("Plays Unknown Appear Anim on open. Options are snapped to the end state so choice buttons are clickable immediately.")]
    [SerializeField] private Animator panelOpenAnimator;
    [Tooltip("CanvasGroup on the options list (animated alpha in Unknown Appear Anim). Auto-resolved from Option Choices Layout Group when empty.")]
    [SerializeField] private CanvasGroup optionChoicesCanvasGroup;

    private static readonly int AppearAnimStateHash = Animator.StringToHash("Unknown Appear Anim");

    private RectTransform _runtimeChoicesRect;
    private UnknownMapEventSO _pendingOptionEvent;
    private UnknownMapEventOptionEntry _pendingOptionEntry;

    private UnknownMapEventSO _currentEvent;
    private MapGrid _pendingGrid;
    private Vector2Int _pendingPlayerCell;
    private int _pendingMovesTaken;
    private Coroutine _dieChoiceRoutine;
    private Coroutine _openPresentationRoutine;
    private CanvasGroup _optionsGroupClickabilityOverride;
    private bool _enforceOptionsClickable;
    private UnknownMapEventSO _pendingChainedEvent;
    private bool _openedChainedEventThisOutcome;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (titleTextMesh == null && descriptionTextMesh == null)
            Debug.LogError(
                "MapUnknownEventPanel: assign Title Text Mesh and/or Description Text Mesh so the event copy can be shown.",
                this);

        if (optionRowPrefab != null && optionRowPrefab.GetComponent<UnknownMapEventChoiceRowView>() == null)
            Debug.LogError(
                "MapUnknownEventPanel: Option Row Prefab root must have UnknownMapEventChoiceRowView.",
                this);

        if (dieChoicePopup == null)
            Debug.LogWarning(
                "MapUnknownEventPanel: assign Die Choice Popup for options that require picking a die.",
                this);

        if (optionChoicesLayoutGroup != null && optionChoicesLayoutGroup.transform is not RectTransform)
            Debug.LogError("MapUnknownEventPanel: Option Choices Layout Group must live on a RectTransform.", this);

        if (optionChoicesCanvasGroup == null && optionChoicesLayoutGroup != null)
            optionChoicesCanvasGroup = optionChoicesLayoutGroup.GetComponent<CanvasGroup>();
    }
#endif

    private void Update()
    {
        if (!_enforceOptionsClickable)
            return;

        ForceOptionChoicesInteractable(_optionsGroupClickabilityOverride);
    }

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        if (panelOpenAnimator == null && root != null)
            panelOpenAnimator = root.GetComponent<Animator>();
        if (optionChoicesCanvasGroup == null && optionChoicesLayoutGroup != null)
            optionChoicesCanvasGroup = optionChoicesLayoutGroup.GetComponent<CanvasGroup>();
        root.SetActive(false);
    }

    private RectTransform EffectiveChoicesRect
    {
        get
        {
            if (optionChoicesLayoutGroup != null)
                return optionChoicesLayoutGroup.transform as RectTransform;
            return _runtimeChoicesRect;
        }
    }

    private void EnsureRuntimeChoicesLayout()
    {
        if (optionChoicesLayoutGroup != null || _runtimeChoicesRect != null)
            return;
        var parent = root != null ? root.transform : transform;
        var go = new GameObject("UnknownOptionList", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.12f);
        rt.anchorMax = new Vector2(0.92f, 0.42f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        _runtimeChoicesRect = rt;
    }

    /// <summary>
    /// Shows Unknown Event UI for the drawn event, or starts combat immediately when <see cref="UnknownMapEventSO.triggersCombat"/> is set.
    /// Returns false if run state is invalid (tile should stay unconsumed).
    /// </summary>
    public bool TryOpenPanel(UnknownMapEventSO unknownEvent, MapGrid grid, Vector2Int playerCell, int movesTaken)
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("MapUnknownEventPanel: RunManager missing.", this);
            return false;
        }

        if (!RunManager.Instance.UseMapBasedRun)
        {
            Debug.LogError("MapUnknownEventPanel: not in map-based run.", this);
            return false;
        }

        _currentEvent = unknownEvent;
        _pendingGrid = grid;
        _pendingPlayerCell = playerCell;
        _pendingMovesTaken = movesTaken;

        if (_currentEvent != null)
            Debug.Log($"Unknown tile: {_currentEvent.DisplayLabel}" +
                      (string.IsNullOrEmpty(_currentEvent.description) ? "" : $" — {_currentEvent.description}"));
        else
            Debug.Log("Unknown tile: no eligible unknown events for this act (filtered pool empty or unconfigured).");

        if (_currentEvent != null && _currentEvent.triggersCombat)
            return TryStartCombatOnEnter();

        if (root == null)
            root = gameObject;

        ActivateSelfAndAncestors(root.transform);
        root.SetActive(true);

        PopulateStaticUi(_currentEvent);

        EnsureRuntimeChoicesLayout();
        var listRoot = EffectiveChoicesRect;
        if (listRoot == null)
        {
            Debug.LogError(
                "MapUnknownEventPanel: assign Option Choices Layout Group (Vertical/Horizontal/Grid) or ensure root is a valid UI parent for the runtime list.",
                this);
            if (root != null)
                root.SetActive(false);
            return false;
        }

        ClearOptionRows();

        if (optionRowPrefab == null)
        {
            Debug.LogError(
                "MapUnknownEventPanel: assign Option Row Prefab (root with UnknownMapEventChoiceRowView: Button + TMP label).",
                this);
            if (root != null)
                root.SetActive(false);
            return false;
        }

        if (_currentEvent != null && _currentEvent.HasChoiceOptions)
        {
            BuildOptionRowsForEvent(_currentEvent, listRoot);
            if (listRoot.childCount == 0)
            {
                Debug.LogWarning(
                    $"MapUnknownEventPanel: '{_currentEvent.DisplayLabel}' has choice rows but none passed enabledWhen — adding Leave.",
                    this);
                AddDismissRow(listRoot, "Leave");
            }
        }
        else
        {
            var dismissLabel = _currentEvent == null ? "Close" : "Leave";
            AddDismissRow(listRoot, dismissLabel);
        }

        FinalizeOpenPresentation(listRoot);
        return true;
    }

    private void FinalizeOpenPresentation(RectTransform listRoot)
    {
        if (_openPresentationRoutine != null)
        {
            StopCoroutine(_openPresentationRoutine);
            _openPresentationRoutine = null;
        }

        _openPresentationRoutine = StartCoroutine(CoOpenPresentation(listRoot));
    }

    /// <summary>
    /// Unknown Appear Anim keeps Options Layout <see cref="CanvasGroup.alpha"/> at 0 until ~0.65s, which blocks clicks.
    /// <see cref="Update"/> re-applies interactable state after the animator and before UI input while this runs.
    /// </summary>
    private IEnumerator CoOpenPresentation(RectTransform listRoot)
    {
        if (listRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);

        Canvas.ForceUpdateCanvases();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        var optionsGroup = ResolveOptionChoicesCanvasGroup();
        _optionsGroupClickabilityOverride = optionsGroup;
        _enforceOptionsClickable = true;
        ForceOptionChoicesInteractable(optionsGroup);

        var animator = panelOpenAnimator != null ? panelOpenAnimator : root?.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.Play(AppearAnimStateHash, 0, 0f);
            animator.Update(0f);
            ForceOptionChoicesInteractable(optionsGroup);

            while (animator.isActiveAndEnabled)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash != AppearAnimStateHash || state.normalizedTime >= 1f)
                    break;

                yield return null;
            }
        }

        _enforceOptionsClickable = false;
        ForceOptionChoicesInteractable(optionsGroup);
        _openPresentationRoutine = null;
    }

    private CanvasGroup ResolveOptionChoicesCanvasGroup()
    {
        if (optionChoicesCanvasGroup != null)
            return optionChoicesCanvasGroup;
        return optionChoicesLayoutGroup != null
            ? optionChoicesLayoutGroup.GetComponent<CanvasGroup>()
            : null;
    }

    private static void ForceOptionChoicesInteractable(CanvasGroup optionsGroup)
    {
        if (optionsGroup == null)
            return;

        optionsGroup.alpha = 1f;
        optionsGroup.interactable = true;
        optionsGroup.blocksRaycasts = true;

        var rows = optionsGroup.GetComponentsInChildren<UnknownMapEventChoiceRowView>(true);
        for (var i = 0; i < rows.Length; i++)
        {
            var rowButton = rows[i].GetComponent<Button>();
            if (rowButton != null)
                rowButton.interactable = true;
        }
    }

    private bool TryStartCombatOnEnter()
    {
        if (RunManager.Instance == null || _currentEvent == null || _pendingGrid == null)
        {
            Debug.LogError("MapUnknownEventPanel.TryStartCombatOnEnter: missing run, event, or grid.", this);
            return false;
        }

        if (!RunManager.Instance.TryBeginUnknownMapEventCombat(
                _currentEvent,
                _pendingGrid,
                _pendingPlayerCell,
                _pendingMovesTaken))
        {
            Debug.LogError(
                "MapUnknownEventPanel: triggersCombat but no enemy could be resolved.",
                _currentEvent);
            return false;
        }

        _currentEvent = null;
        _pendingGrid = null;
        return true;
    }

    private void PopulateStaticUi(UnknownMapEventSO unknownEvent)
    {
        string title;
        string body;
        if (unknownEvent != null)
        {
            title = unknownEvent.DisplayLabel;
            body = unknownEvent.description ?? string.Empty;
        }
        else
        {
            title = "Unknown";
            body = "No eligible unknown events for this act.";
        }

        if (titleTextMesh != null)
            titleTextMesh.text = title;
        if (descriptionTextMesh != null)
        {
            if (titleTextMesh != null)
                descriptionTextMesh.text = body;
            else
                descriptionTextMesh.text = string.IsNullOrEmpty(body) ? title : $"{title}\n\n{body}";
        }
        else if (titleTextMesh != null)
            titleTextMesh.text = string.IsNullOrEmpty(body) ? title : $"{title}\n\n{body}";

        if (eventArtImage != null)
        {
            var sprite = unknownEvent != null ? unknownEvent.eventArt : null;
            eventArtImage.sprite = sprite;
            eventArtImage.enabled = sprite != null;
        }
    }

    private void BuildOptionRowsForEvent(UnknownMapEventSO ev, RectTransform listRoot)
    {
        if (ev.choices == null)
            return;

        var evalCtx = new UnknownMapEventEvaluationContext(RunManager.Instance, _pendingGrid, _pendingPlayerCell, _pendingMovesTaken);
        for (var i = 0; i < ev.choices.Length; i++)
        {
            var entry = ev.choices[i];
            if (entry == null)
                continue;
            if (!UnknownMapEventConditionEvaluator.AllPass(entry.enabledWhen, evalCtx))
                continue;

            var captured = entry;
            var label = UnknownMapEventLabelUtility.ResolveOptionLabel(captured);
            WireOptionRow(CreateOptionRowInstance(listRoot), label, () => OnOptionChosen(ev, captured));
        }
    }

    private void AddDismissRow(RectTransform listRoot, string label)
    {
        WireOptionRow(CreateOptionRowInstance(listRoot), label, Hide);
    }

    private GameObject CreateOptionRowInstance(RectTransform listRoot)
    {
        if (optionRowPrefab == null)
            return null;
        return Instantiate(optionRowPrefab, listRoot);
    }

    private static void WireOptionRow(GameObject rowInstance, string label, UnityAction onClick)
    {
        if (rowInstance == null)
            return;

        var row = rowInstance.GetComponent<UnknownMapEventChoiceRowView>();
        if (row == null)
        {
            Debug.LogError(
                "MapUnknownEventPanel: option row prefab root must have UnknownMapEventChoiceRowView.",
                rowInstance);
            return;
        }

        row.Bind(label, onClick);
    }

    private void OnOptionChosen(UnknownMapEventSO ev, UnknownMapEventOptionEntry entry)
    {
        if (RunManager.Instance == null || ev == null || entry == null)
        {
            Hide();
            return;
        }

        if (entry.outcome is UnknownMapEventOutcomeAfterDieChoice dieChoiceOutcome)
        {
            if (dieChoicePopup == null)
            {
                Debug.LogError(
                    "MapUnknownEventPanel: option requires die choice but Die Choice Popup is not assigned.",
                    this);
                return;
            }

            _pendingOptionEvent = ev;
            _pendingOptionEntry = entry;
            var title = UnknownMapEventLabelUtility.ResolveOptionLabel(entry);
            dieChoicePopup.Show(
                title,
                dieChoiceOutcome.DiePassesFilter,
                OnDieChoiceCommitted,
                OnDieChoiceCancelled);
            return;
        }

        ExecuteOptionOutcome(ev, entry, chosenDie: null);
        if (!_openedChainedEventThisOutcome)
            Hide();
    }

    public void RequestOpenChainedEvent(UnknownMapEventSO nextEvent)
    {
        if (nextEvent == null)
        {
            Debug.LogError("MapUnknownEventPanel.RequestOpenChainedEvent: nextEvent is null.", this);
            return;
        }

        _pendingChainedEvent = nextEvent;
    }

    private void OpenChainedEvent(UnknownMapEventSO nextEvent)
    {
        if (nextEvent == null)
            return;

        if (nextEvent.triggersCombat && RunManager.Instance != null && _pendingGrid != null)
        {
            if (RunManager.Instance.TryBeginUnknownMapEventCombat(
                    nextEvent,
                    _pendingGrid,
                    _pendingPlayerCell,
                    _pendingMovesTaken))
            {
                Hide();
                return;
            }

            Debug.LogError(
                "MapUnknownEventPanel.OpenChainedEvent: chained event triggersCombat but no enemy could be resolved.",
                nextEvent);
            Hide();
            return;
        }

        _currentEvent = nextEvent;
        PopulateStaticUi(_currentEvent);

        EnsureRuntimeChoicesLayout();
        var listRoot = EffectiveChoicesRect;
        if (listRoot == null)
        {
            Debug.LogError("MapUnknownEventPanel.OpenChainedEvent: missing options list root.", this);
            Hide();
            return;
        }

        ClearOptionRows();

        if (_currentEvent.HasChoiceOptions)
        {
            BuildOptionRowsForEvent(_currentEvent, listRoot);
            if (listRoot.childCount == 0)
            {
                Debug.LogWarning(
                    $"MapUnknownEventPanel: chained event '{_currentEvent.DisplayLabel}' has no enabled options — adding Leave.",
                    this);
                AddDismissRow(listRoot, "Leave");
            }
        }
        else
        {
            AddDismissRow(listRoot, "Leave");
        }

        OpenChainedEventImmediately(listRoot);
    }

    private void OpenChainedEventImmediately(RectTransform listRoot)
    {
        if (_openPresentationRoutine != null)
        {
            StopCoroutine(_openPresentationRoutine);
            _openPresentationRoutine = null;
        }

        _enforceOptionsClickable = false;

        if (listRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);

        Canvas.ForceUpdateCanvases();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        ForceOptionChoicesInteractable(ResolveOptionChoicesCanvasGroup());
    }

    private void TryConsumePendingChainedEvent()
    {
        if (_pendingChainedEvent == null)
            return;

        OpenChainedEvent(_pendingChainedEvent);
        _pendingChainedEvent = null;
        _openedChainedEventThisOutcome = true;
    }

    private void OnDieChoiceCommitted(DieAssetSO die)
    {
        if (_dieChoiceRoutine != null)
            StopCoroutine(_dieChoiceRoutine);
        _dieChoiceRoutine = StartCoroutine(CoFinishDieChoice(die));
    }

    private void OnDieChoiceCancelled()
    {
        if (_dieChoiceRoutine != null)
        {
            StopCoroutine(_dieChoiceRoutine);
            _dieChoiceRoutine = null;
        }

        _pendingOptionEvent = null;
        _pendingOptionEntry = null;
    }

    IEnumerator CoFinishDieChoice(DieAssetSO die)
    {
        var ev = _pendingOptionEvent;
        var entry = _pendingOptionEntry;
        _pendingOptionEvent = null;
        _pendingOptionEntry = null;
        _dieChoiceRoutine = null;

        if (RunManager.Instance == null || ev == null || entry == null || die == null)
        {
            dieChoicePopup?.Hide();
            Hide();
            yield break;
        }

        _openedChainedEventThisOutcome = false;
        _pendingChainedEvent = null;

        if (entry.outcome is UnknownMapEventOutcomeAfterDieChoice afterDieChoice)
        {
            yield return CoRunAfterDieChoiceSteps(ev, entry, afterDieChoice, die);
            dieChoicePopup?.Hide();
            if (entry.registerEventCompletedOnPick)
                RunManager.Instance.RegisterUnknownMapEventCompleted(ev.ResolvedEventId);
            TryConsumePendingChainedEvent();
            if (!_openedChainedEventThisOutcome)
                Hide();
            yield break;
        }

        ExecuteOptionOutcome(ev, entry, die);
        dieChoicePopup?.Hide();
        if (!_openedChainedEventThisOutcome)
            Hide();
    }

    IEnumerator CoRunAfterDieChoiceSteps(
        UnknownMapEventSO ev,
        UnknownMapEventOptionEntry entry,
        UnknownMapEventOutcomeAfterDieChoice afterDieChoice,
        DieAssetSO die)
    {
        if (afterDieChoice.steps == null || dieChoicePopup == null)
            yield break;

        var evalCtx = new UnknownMapEventEvaluationContext(
            RunManager.Instance,
            _pendingGrid,
            _pendingPlayerCell,
            _pendingMovesTaken);
        var outcomeCtx = new UnknownMapEventOutcomeContext(
            evalCtx,
            ev,
            _pendingGrid,
            _pendingPlayerCell,
            _pendingMovesTaken,
            die,
            host: this);

        var usedFaceSlots = new HashSet<int>();
        var faceSwapPreviewSlots = new List<int>();

        for (var i = 0; i < afterDieChoice.steps.Count; i++)
        {
            var step = afterDieChoice.steps[i];
            if (step == null)
                continue;

            if (step is UnknownMapEventOutcomeAddCurseFaceToChosenDie addCurse)
            {
                if (addCurse.curseFace == null)
                {
                    Debug.LogError("MapUnknownEventPanel: AddCurseFaceToChosenDie missing curseFace.", this);
                    continue;
                }

                if (addCurse.TrySwapAtRandomSlot(die, addCurse.curseFace, usedFaceSlots, out var curseSlot))
                {
                    usedFaceSlots.Add(curseSlot);
                    faceSwapPreviewSlots.Add(curseSlot);
                }
                else
                {
                    Debug.LogWarning("MapUnknownEventPanel: could not place curse face on chosen die.", this);
                }

                continue;
            }

            if (step is UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnChosenDie replaceFace)
            {
                var face = replaceFace.PickFaceForDie(die);
                if (face == null)
                {
                    Debug.LogWarning($"MapUnknownEventPanel: no {replaceFace.rarity} face for chosen die.", this);
                    continue;
                }

                if (replaceFace.TrySwapAtRandomSlot(die, face, usedFaceSlots, out var faceSlot))
                {
                    usedFaceSlots.Add(faceSlot);
                    faceSwapPreviewSlots.Add(faceSlot);
                }
                else
                {
                    Debug.LogWarning($"MapUnknownEventPanel: could not place {replaceFace.rarity} face on chosen die.", this);
                }

                continue;
            }

            if (step is UnknownMapEventOutcomeReplaceFirstCurseOnChosenDieWithBaseForDieLine replaceCurse)
            {
                if (replaceCurse.TryReplaceFirstCurse(die, out var curseSlot))
                    faceSwapPreviewSlots.Add(curseSlot);
                else
                    Debug.LogWarning("MapUnknownEventPanel: no curse face replaced on chosen die.", this);
                continue;
            }

            step.Execute(outcomeCtx);

            DieAssetSO appearDie = null;
            if (UnknownMapEventDieChoiceUtility.StepMutatesDeckComposition(step))
            {
                if (UnknownMapEventDieChoiceUtility.StepAddsDeckDie(step))
                    appearDie = PlayerDataContainer.Instance?.LastAddedDeckDie;
                dieChoicePopup.RefreshDiceTray(appearDie);
            }

            var acknowledgeDie = appearDie != null ? appearDie : die;
            if (step is UnknownMapEventOutcomeRemoveChosenDeckDie)
                yield return dieChoicePopup.CoWaitAfterPopupAction();
            else
                yield return dieChoicePopup.CoAcknowledgeActionOnDie(acknowledgeDie);
        }

        if (faceSwapPreviewSlots.Count > 0)
            yield return dieChoicePopup.CoResolveFaceSwapsOnDie(die, faceSwapPreviewSlots);

        TryConsumePendingChainedEvent();
    }

    private void ExecuteOptionOutcome(UnknownMapEventSO ev, UnknownMapEventOptionEntry entry, DieAssetSO chosenDie)
    {
        _openedChainedEventThisOutcome = false;
        _pendingChainedEvent = null;

        var evalCtx = new UnknownMapEventEvaluationContext(
            RunManager.Instance,
            _pendingGrid,
            _pendingPlayerCell,
            _pendingMovesTaken);
        var outcomeCtx = new UnknownMapEventOutcomeContext(
            evalCtx,
            ev,
            _pendingGrid,
            _pendingPlayerCell,
            _pendingMovesTaken,
            chosenDie,
            host: this);

        if (entry.outcome != null)
            entry.outcome.Execute(outcomeCtx);

        if (entry.registerEventCompletedOnPick)
            RunManager.Instance.RegisterUnknownMapEventCompleted(ev.ResolvedEventId);

        TryConsumePendingChainedEvent();
    }

    private void ClearOptionRows()
    {
        ClearOptionChildren(EffectiveChoicesRect);
    }

    private static void ClearOptionChildren(RectTransform listRoot)
    {
        if (listRoot == null)
            return;
        for (var i = listRoot.childCount - 1; i >= 0; i--)
        {
            var c = listRoot.GetChild(i);
            if (c != null)
                Destroy(c.gameObject);
        }
    }

    /// <summary>Closes the panel (e.g. map regenerated).</summary>
    public void Hide()
    {
        _enforceOptionsClickable = false;

        if (_openPresentationRoutine != null)
        {
            StopCoroutine(_openPresentationRoutine);
            _openPresentationRoutine = null;
        }

        if (_dieChoiceRoutine != null)
        {
            StopCoroutine(_dieChoiceRoutine);
            _dieChoiceRoutine = null;
        }

        _currentEvent = null;
        _pendingGrid = null;
        _pendingOptionEvent = null;
        _pendingOptionEntry = null;
        _pendingChainedEvent = null;
        _openedChainedEventThisOutcome = false;
        dieChoicePopup?.Hide();
        ClearOptionRows();
        if (root != null)
            root.SetActive(false);
    }

    private static void ActivateSelfAndAncestors(Transform t)
    {
        if (t == null)
            return;
        if (t.parent != null)
            ActivateSelfAndAncestors(t.parent);
        t.gameObject.SetActive(true);
    }
}
