using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Map-only panel for <see cref="MapEventType.Unknown"/> tiles.
/// Legacy combat-on-enter is unchanged; otherwise the player always dismisses via option rows built from <see cref="optionRowPrefab"/>.
/// </summary>
public sealed class MapUnknownEventPanel : MonoBehaviour
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

    private RectTransform _runtimeChoicesRect;

    private UnknownMapEventSO _currentEvent;
    private MapGrid _pendingGrid;
    private Vector2Int _pendingPlayerCell;
    private int _pendingMovesTaken;

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

        if (optionChoicesLayoutGroup != null && optionChoicesLayoutGroup.transform is not RectTransform)
            Debug.LogError("MapUnknownEventPanel: Option Choices Layout Group must live on a RectTransform.", this);
    }
#endif

    private void Awake()
    {
        if (root == null)
            root = gameObject;
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
    /// Shows Unknown Event UI for the drawn event, or starts legacy combat immediately when configured.
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

        if (_currentEvent != null && _currentEvent.triggersCombat && !_currentEvent.HasChoiceOptions)
            return TryStartLegacyCombatOnEnter();

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

        return true;
    }

    private bool TryStartLegacyCombatOnEnter()
    {
        if (RunManager.Instance == null || _currentEvent == null || _pendingGrid == null)
        {
            Debug.LogError("MapUnknownEventPanel.TryStartLegacyCombatOnEnter: missing run, event, or grid.", this);
            return false;
        }

        var enemy = _currentEvent.ResolveEnemyForCombat(RunManager.Instance);
        if (enemy == null)
        {
            Debug.LogError(
                "MapUnknownEventPanel: legacy unknown triggers combat on enter but no enemy could be resolved.",
                _currentEvent);
            return false;
        }

        if (_currentEvent.registerCompletedOnLegacyCombatEnter)
            RunManager.Instance.RegisterUnknownMapEventCompleted(_currentEvent.ResolvedEventId);

        RunManager.Instance.PersistAndLoadFightSceneWithEnemy(
            _pendingGrid,
            _pendingPlayerCell,
            _pendingMovesTaken,
            enemy,
            _currentEvent.countsAsBossTileForRunProgression);

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
            var label = string.IsNullOrWhiteSpace(captured.label) ? "Choose" : captured.label.Trim();
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

        var evalCtx = new UnknownMapEventEvaluationContext(
            RunManager.Instance,
            _pendingGrid,
            _pendingPlayerCell,
            _pendingMovesTaken);
        var outcomeCtx = new UnknownMapEventOutcomeContext(evalCtx, ev, _pendingGrid, _pendingPlayerCell, _pendingMovesTaken);

        if (entry.outcome != null)
            entry.outcome.Execute(outcomeCtx);

        if (entry.registerEventCompletedOnPick)
            RunManager.Instance.RegisterUnknownMapEventCompleted(ev.ResolvedEventId);

        Hide();
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
        _currentEvent = null;
        _pendingGrid = null;
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
