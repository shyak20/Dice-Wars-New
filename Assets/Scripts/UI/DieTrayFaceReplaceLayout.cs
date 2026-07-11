using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Deck tray that swaps a compact die button for <see cref="DieFaceSpreadView"/> during face replacement.
/// Hover tooltips use <see cref="DieTooltipOverlayUI"/> only outside replace mode.
/// </summary>
/// <remarks>
/// <b>Unity setup (Face Picker + Shop Die Choice popup)</b>
/// <list type="number">
/// <item>Add this component on the dice tray container (or a child with the layout group).</item>
/// <item>Assign <c>diceLayoutContainer</c> to the object with <see cref="HorizontalOrVerticalLayoutGroup"/>.</item>
/// <item>Assign existing compact <c>dieButtonPrefab</c> (<see cref="DiceTrayButtonView"/> + <c>Button</c>).</item>
/// <item>Author a spread prefab: root <see cref="DieFaceSpreadView"/> + <c>Animator</c> (Face Replace CTRL) + optional rule-error UI;
/// each of 6 children needs <see cref="DieFaceSpreadSlotView"/> + <see cref="UIRewardSlot"/> + <c>Button</c>, <c>faceIndex</c> 0–5.</item>
/// <item>Assign fallback <c>dieFaceSpreadPrefab</c> on the layout for dies missing <see cref="DieAssetSO.faceSpreadViewPrefab"/>.</item>
/// <item>Wire <see cref="FacePickerView"/> / <see cref="ShopDieChoicePopupView"/> <c>trayLayout</c> to this component.</item>
/// </list>
/// </remarks>
public sealed class DieTrayFaceReplaceLayout : MonoBehaviour
{
    [SerializeField] private Transform diceLayoutContainer;
    [SerializeField] private GameObject dieButtonPrefab;
    [Tooltip("Fallback when a die asset has no faceSpreadViewPrefab assigned.")]
    [SerializeField] private GameObject dieFaceSpreadPrefab;
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;
    [SerializeField, Min(0f)] private float spreadDeselectDuration = 0.35f;

    private readonly Dictionary<DieAssetSO, TrayEntry> _entries = new();
    private readonly Dictionary<DieAssetSO, Coroutine> _collapseRoutines = new();
    private Coroutine _prewarmRoutine;
    private bool _hoverTooltipsEnabled = true;
    private DieAssetSO _activeSpreadDie;
    private DieAssetSO _pinnedTooltipDie;
    private Action<DieAssetSO> _onDieClicked;
    private Func<DieAssetSO, bool> _interactableFilter;

    private sealed class TrayEntry
    {
        public RectTransform SlotRoot;
        public GameObject ButtonObject;
        public DiceTrayButtonView ButtonView;
        public Button Button;
        public DieFaceSpreadView SpreadView;
        public GameObject SpreadPrefabUsed;
    }

    private void Awake()
    {
        if (diceLayoutContainer == null)
            diceLayoutContainer = transform;
        if (dieButtonPrefab == null)
            throw new InvalidOperationException($"DieTrayFaceReplaceLayout on '{name}': assign dieButtonPrefab.");

        if (dieFaceSpreadPrefab != null)
            ValidateSpreadPrefab(dieFaceSpreadPrefab, $"DieTrayFaceReplaceLayout on '{name}' (dieFaceSpreadPrefab fallback)");
    }

    public Transform DiceContainer => diceLayoutContainer;

    public HorizontalOrVerticalLayoutGroup DiceLayoutGroup =>
        diceLayoutContainer != null ? diceLayoutContainer.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;

    public void SetHoverTooltipsEnabled(bool enabled)
    {
        _hoverTooltipsEnabled = enabled;
        if (!enabled)
        {
            _pinnedTooltipDie = null;
            dieTooltipOverlay?.Hide();
        }
    }

    public void PinDieTooltip(DieAssetSO die)
    {
        if (die == null)
            return;

        _pinnedTooltipDie = die;
        SetSelectedDie(die);
        dieTooltipOverlay?.ShowDie(die, false, null, GetDieIconRect(die));
    }

    public void ClearPinnedDieTooltip()
    {
        _pinnedTooltipDie = null;
        ClearButtonSelections();
        dieTooltipOverlay?.Hide();
    }

    public void Rebuild(
        IReadOnlyList<DieAssetSO> dice,
        Func<DieAssetSO, bool> includeFilter = null,
        Func<DieAssetSO, bool> interactableFilter = null,
        Action<DieAssetSO> onDieClicked = null)
    {
        StopPrewarmSpreads();
        CollapseAllFaceReplaceImmediate();
        _onDieClicked = onDieClicked;
        _interactableFilter = interactableFilter;
        _pinnedTooltipDie = null;

        foreach (Transform c in diceLayoutContainer)
            Destroy(c.gameObject);
        _entries.Clear();
        _activeSpreadDie = null;

        if (dice == null)
            return;

        for (var i = 0; i < dice.Count; i++)
        {
            var die = dice[i];
            if (die == null)
                continue;
            if (includeFilter != null && !includeFilter(die))
                continue;

            var slotGo = new GameObject($"DieTraySlot_{die.name}", typeof(RectTransform));
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.SetParent(diceLayoutContainer, false);
            slotRt.localScale = Vector3.one;
            CenterRectInParent(slotRt);

            var buttonGo = Instantiate(dieButtonPrefab, slotRt);
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            if (buttonRt != null)
                CenterRectInParent(buttonRt);
            var txt = buttonGo.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = die.dieName;

            var view = buttonGo.GetComponent<DiceTrayButtonView>();
            if (view == null)
                throw new InvalidOperationException($"DieTrayFaceReplaceLayout: dieButtonPrefab needs DiceTrayButtonView on '{dieButtonPrefab.name}'.");

            view.SetIcon(die.uiIcon);
            view.SetSelected(false);
            view.SetSelectedIconShakeEnabled(false);

            var btn = buttonGo.GetComponent<Button>();
            if (btn == null)
                throw new InvalidOperationException($"DieTrayFaceReplaceLayout: dieButtonPrefab needs Button on '{dieButtonPrefab.name}'.");

            var captured = die;
            btn.onClick.AddListener(() => _onDieClicked?.Invoke(captured));
            RegisterDieHover(btn, captured);

            var interactable = interactableFilter == null || interactableFilter(die);
            btn.interactable = interactable;

            _entries[die] = new TrayEntry
            {
                SlotRoot = slotRt,
                ButtonObject = buttonGo,
                ButtonView = view,
                Button = btn,
                SpreadView = null
            };
        }
    }

    public void SetDieInteractable(DieAssetSO die, bool interactable)
    {
        if (die == null || !_entries.TryGetValue(die, out var entry) || entry.Button == null)
            return;
        entry.Button.interactable = interactable;
        if (!interactable && entry.ButtonView != null)
            entry.ButtonView.SetSelected(false);
    }

    public void RefreshInteractable(Func<DieAssetSO, bool> interactableFilter = null)
    {
        if (interactableFilter != null)
            _interactableFilter = interactableFilter;

        foreach (var kv in _entries)
        {
            var interactable = _interactableFilter == null || _interactableFilter(kv.Key);
            SetDieInteractable(kv.Key, interactable);
        }
    }

    public bool IsSpreadPrewarmed(DieAssetSO die) =>
        die != null && _entries.TryGetValue(die, out var entry) && entry.SpreadView != null;

    public void StartPrewarmSpreadsForCurrentEntries()
    {
        StopPrewarmSpreads();
        if (_entries.Count == 0)
            return;
        if (!isActiveAndEnabled)
            return;

        var dice = new List<DieAssetSO>(_entries.Keys);
        _prewarmRoutine = StartCoroutine(PrewarmSpreadsRoutine(dice));
    }

    public void StopPrewarmSpreads()
    {
        if (_prewarmRoutine == null)
            return;

        StopCoroutine(_prewarmRoutine);
        _prewarmRoutine = null;
    }

    public IEnumerator PrewarmSpreadsRoutine(IReadOnlyList<DieAssetSO> dice)
    {
        if (dice == null)
            yield break;

        for (var i = 0; i < dice.Count; i++)
        {
            var die = dice[i];
            if (die != null && _entries.ContainsKey(die))
                EnsureSpreadInstance(die);

            yield return null;
        }

        _prewarmRoutine = null;
    }

    public void ClearButtonSelections()
    {
        foreach (var kv in _entries)
            kv.Value.ButtonView?.SetSelected(false);
    }

    public void SetSelectedDie(DieAssetSO die)
    {
        foreach (var kv in _entries)
            kv.Value.ButtonView?.SetSelected(kv.Key == die);
    }

    public RectTransform GetDieIconRect(DieAssetSO die)
    {
        if (die == null || !_entries.TryGetValue(die, out var entry))
            return null;
        return entry.ButtonView != null ? entry.ButtonView.IconRectTransform : null;
    }

    public void SelectDieForFaceReplace(
        DieAssetSO die,
        DieFaceSO targetFace,
        Func<int, bool> slotAllowed,
        Action<int, DieFaceSO, UIRewardSlot> onSlotPicked)
    {
        if (die == null || targetFace == null)
            return;

        _pinnedTooltipDie = null;
        dieTooltipOverlay?.Hide();

        if (_activeSpreadDie != null && _activeSpreadDie != die)
            BeginCollapseSpread(_activeSpreadDie, null);

        if (!_entries.TryGetValue(die, out var entry))
            return;

        StopCollapseRoutine(die);

        foreach (var kv in _entries)
            kv.Value.ButtonView?.SetSelected(kv.Key == die);

        entry.ButtonObject.SetActive(false);

        if (!EnsureSpreadInstance(die))
            return;

        entry.SpreadView.gameObject.SetActive(true);

        _activeSpreadDie = die;
        entry.SpreadView.Bind(die, targetFace, slotAllowed, onSlotPicked, dieTooltipOverlay);
        entry.SpreadView.SetSelected(true);
    }

    public void CollapseAllFaceReplace(Action onComplete = null)
    {
        if (_activeSpreadDie == null || !HasVisibleSpread(_activeSpreadDie))
        {
            ClearSpreadSelectionState();
            onComplete?.Invoke();
            return;
        }

        var die = _activeSpreadDie;
        BeginCollapseSpread(die, () =>
        {
            ClearSpreadSelectionState();
            onComplete?.Invoke();
        });
    }

    public void CollapseAllFaceReplaceImmediate()
    {
        StopAllCollapseRoutines();

        foreach (var kv in _entries)
            HideSpreadImmediate(kv.Value);

        ClearSpreadSelectionState();
    }

    public void NotifyFaceReplacementRuleError(DieAssetSO die = null, int slotIndex = -1) =>
        dieTooltipOverlay?.ShowFaceReplacementRuleError(die, slotIndex);

    public void SetAllSpreadSlotsInteractable(bool interactable)
    {
        if (_activeSpreadDie == null
            || !_entries.TryGetValue(_activeSpreadDie, out var entry)
            || entry.SpreadView == null)
            return;

        entry.SpreadView.SetAllSlotsInteractable(interactable);
    }

    void BeginCollapseSpread(DieAssetSO die, Action onComplete)
    {
        if (die == null || !_entries.TryGetValue(die, out var entry) || entry.SpreadView == null || !entry.SpreadView.gameObject.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        StopCollapseRoutine(die);
        _collapseRoutines[die] = StartCoroutine(CoCollapseSpreadForDie(die, onComplete));
    }

    IEnumerator CoCollapseSpreadForDie(DieAssetSO die, Action onComplete)
    {
        if (!_entries.TryGetValue(die, out var entry) || entry.SpreadView == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        entry.SpreadView.SetAllSlotsInteractable(false);
        entry.SpreadView.SetSelected(false);

        if (spreadDeselectDuration > 0f)
            yield return new WaitForSecondsRealtime(spreadDeselectDuration);

        if (!_entries.TryGetValue(die, out entry) || entry.SpreadView == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        HideSpreadImmediate(entry);

        if (_activeSpreadDie == die)
            _activeSpreadDie = null;

        _collapseRoutines.Remove(die);
        onComplete?.Invoke();
    }

    static void HideSpreadImmediate(TrayEntry entry)
    {
        if (entry.SpreadView != null)
            entry.SpreadView.gameObject.SetActive(false);

        if (entry.ButtonObject != null)
            entry.ButtonObject.SetActive(true);
    }

    void ClearSpreadSelectionState()
    {
        _activeSpreadDie = null;
        ClearButtonSelections();
    }

    void StopCollapseRoutine(DieAssetSO die)
    {
        if (die == null || !_collapseRoutines.TryGetValue(die, out var routine))
            return;

        if (routine != null)
            StopCoroutine(routine);

        _collapseRoutines.Remove(die);
    }

    void StopAllCollapseRoutines()
    {
        foreach (var kv in _collapseRoutines)
        {
            if (kv.Value != null)
                StopCoroutine(kv.Value);
        }

        _collapseRoutines.Clear();
    }

    void RegisterDieHover(Button btn, DieAssetSO die)
    {
        if (btn == null || die == null)
            return;

        var go = btn.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            if (!_hoverTooltipsEnabled || HasVisibleSpread(die))
                return;
            dieTooltipOverlay?.ShowDie(die, false, null, GetDieIconRect(die));
        });
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            if (!_hoverTooltipsEnabled || HasVisibleSpread(die))
                return;
            if (_pinnedTooltipDie == die)
                return;
            dieTooltipOverlay?.Hide();
        });
        et.triggers.Add(exit);
    }

    bool HasVisibleSpread(DieAssetSO die) =>
        die != null
        && _entries.TryGetValue(die, out var entry)
        && entry.SpreadView != null
        && entry.SpreadView.gameObject.activeSelf;

    bool EnsureSpreadInstance(DieAssetSO die)
    {
        if (die == null || !_entries.TryGetValue(die, out var entry))
            return false;

        var prefab = ResolveSpreadPrefab(die);

        if (entry.SpreadView != null)
        {
            if (entry.SpreadPrefabUsed == prefab)
                return true;

            Destroy(entry.SpreadView.gameObject);
            entry.SpreadView = null;
            entry.SpreadPrefabUsed = null;
        }

        var spreadGo = Instantiate(prefab, entry.SlotRoot);
        var spreadRt = spreadGo.GetComponent<RectTransform>();
        if (spreadRt != null)
            CenterRectInParent(spreadRt);

        entry.SpreadView = spreadGo.GetComponentInChildren<DieFaceSpreadView>(true);
        if (entry.SpreadView == null)
            throw new InvalidOperationException("DieTrayFaceReplaceLayout: spread instance missing DieFaceSpreadView.");

        entry.SpreadPrefabUsed = prefab;
        spreadGo.SetActive(false);
        return true;
    }

    GameObject ResolveSpreadPrefab(DieAssetSO die)
    {
        if (die == null)
            throw new InvalidOperationException($"DieTrayFaceReplaceLayout on '{name}': cannot resolve spread prefab for null die.");

        var prefab = die.faceSpreadViewPrefab != null ? die.faceSpreadViewPrefab : dieFaceSpreadPrefab;
        ValidateSpreadPrefab(prefab, $"DieTrayFaceReplaceLayout on '{name}' (die '{die.dieName}')");
        return prefab;
    }

    static void ValidateSpreadPrefab(GameObject prefab, string context)
    {
        if (prefab == null)
            throw new InvalidOperationException($"{context}: assign DieAssetSO.faceSpreadViewPrefab or DieTrayFaceReplaceLayout.dieFaceSpreadPrefab fallback.");

        if (prefab.GetComponentInChildren<DieFaceSpreadView>(true) == null)
            throw new InvalidOperationException($"{context}: spread prefab '{prefab.name}' must include DieFaceSpreadView.");
    }

    static void CenterRectInParent(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }
}
