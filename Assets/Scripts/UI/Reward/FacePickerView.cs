using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Single-screen face reward flow:
/// 1) Pick one reward face.
/// 2) Rewards collapse to the picked face.
/// 3) Replacement panel (shared <see cref="DieTooltipOverlayUI"/>) stays visible: choose a die in the tray to
///    switch which die’s faces are shown, then click a face slot to swap and finish.
/// </summary>
public class FacePickerView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject rewardSlotPrefab;

    [Header("Deck dice layout")]
    [SerializeField] private Transform diceLayoutContainer;
    [SerializeField] private GameObject dieButtonPrefab;
    [SerializeField, Min(0f)] private float diceLayoutSpacing = 16f;
    [SerializeField, Min(0.01f)] private float pickTransitionSeconds = 0.25f;
    [SerializeField] private AnimationCurve pickTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Shared die tooltip")]
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;
    [Tooltip("Optional. If assigned, reward-face status hovers use this panel (same behavior as fight scene status tooltip).")]
    [SerializeField] private HoverTooltipPanelUI statusHoverTooltipPanel;

    [Header("Phase Objects")]
    [Tooltip("Active only in phase B: face reward selection (before choosing a reward face).")]
    [SerializeField] private List<GameObject> phaseBObjects = new List<GameObject>();
    [Tooltip("Active only in phase C: face replacement (after choosing a reward face).")]
    [SerializeField] private List<GameObject> phaseCObjects = new List<GameObject>();

    [Header("Win-stage flow (optional)")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button skipButton;

    private readonly List<UIRewardSlot> _rewardSlots = new List<UIRewardSlot>();
    private readonly Dictionary<UIRewardSlot, CanvasGroup> _rewardSlotGroups = new Dictionary<UIRewardSlot, CanvasGroup>();
    private readonly Dictionary<DieAssetSO, DiceTrayButtonView> _diceViews = new Dictionary<DieAssetSO, DiceTrayButtonView>();
    private readonly Dictionary<DieAssetSO, Button> _diceButtons = new Dictionary<DieAssetSO, Button>();

    private Action<DieFaceSO> _onFacePicked;
    private Action<DieAssetSO, int> _onReplaceFaceSlotPicked;
    private Action _onBack;
    private Action _onSkip;
    private Action _onRewindToFacePick;
    private DieFaceSO _selectedRewardFace;
    private DieAssetSO _activeReplacementDie;
    private Coroutine _pickTransitionRoutine;

    private void Awake()
    {
        if (panel == null) Debug.LogError("FacePickerView: assign panel.");
        if (slotContainer == null) Debug.LogError("FacePickerView: assign slotContainer.");
        if (rewardSlotPrefab == null) Debug.LogError("FacePickerView: assign rewardSlotPrefab (UIRewardSlot).");
        if (dieButtonPrefab == null) Debug.LogError("FacePickerView: assign dieButtonPrefab.");
    }

    private void Update()
    {
        // In replacement mode the overlay is always on; do not dismiss on background click.
        if (_selectedRewardFace != null) return;
        if (dieTooltipOverlay == null || dieTooltipOverlay.CurrentDie == null) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current == null) return;

        var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        for (var i = 0; i < hits.Count; i++)
        {
            var t = hits[i].gameObject != null ? hits[i].gameObject.transform : null;
            if (t == null) continue;
            if (diceLayoutContainer != null && t.IsChildOf(diceLayoutContainer)) return;
        }

        _activeReplacementDie = null;
        ClearDiceSelectionVisuals();
        dieTooltipOverlay.Hide();
    }

    public void Show(
        List<DieFaceSO> options,
        Action<DieFaceSO> onFacePicked,
        Action<DieAssetSO, int> onReplaceFaceSlotPicked,
        Action onBack = null,
        Action onSkip = null,
        Action onRewindToFacePick = null)
    {
        if (options == null || options.Count == 0)
        {
            Debug.LogError("FacePickerView: No face options.");
            return;
        }

        _onFacePicked = onFacePicked;
        _onReplaceFaceSlotPicked = onReplaceFaceSlotPicked;
        _onBack = onBack;
        _onSkip = onSkip;
        _onRewindToFacePick = onRewindToFacePick;
        _selectedRewardFace = null;
        _activeReplacementDie = null;

        ConfigureNavButtons();
        RebuildRewardSlots(options);
        RebuildDiceLayout();
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        SetPhaseVisuals(phaseBActive: true);

        panel.SetActive(true);
    }

    public void Hide()
    {
        if (_pickTransitionRoutine != null)
        {
            StopCoroutine(_pickTransitionRoutine);
            _pickTransitionRoutine = null;
        }

        if (panel != null) panel.SetActive(false);
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        SetAllPhaseObjects(false);
    }

    private void RebuildRewardSlots(List<DieFaceSO> options)
    {
        foreach (Transform c in slotContainer)
            Destroy(c.gameObject);
        _rewardSlots.Clear();
        _rewardSlotGroups.Clear();

        foreach (var face in options)
        {
            var go = Instantiate(rewardSlotPrefab, slotContainer);
            var slot = go.GetComponent<UIRewardSlot>();
            if (slot == null)
            {
                Debug.LogError("FacePickerView: rewardSlotPrefab needs UIRewardSlot.");
                Destroy(go);
                continue;
            }

            slot.Bind(face, OnRewardFaceClicked);
            slot.SetStatusHoverTooltipPanel(statusHoverTooltipPanel);
            slot.SetInteractable(true);
            slot.EnsureStandaloneHoverReveal();
            _rewardSlots.Add(slot);
            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            _rewardSlotGroups[slot] = cg;
        }
    }

    private void RebuildDiceLayout()
    {
        if (diceLayoutContainer == null || PlayerDataContainer.Instance?.RuntimeData == null)
            return;

        foreach (Transform c in diceLayoutContainer)
            Destroy(c.gameObject);
        _diceViews.Clear();
        _diceButtons.Clear();

        var layout = diceLayoutContainer.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (layout != null)
            layout.spacing = _selectedRewardFace == null ? 0f : diceLayoutSpacing;

        var deck = PlayerDataContainer.Instance.RuntimeData.currentDeck;
        for (var i = 0; i < deck.Count; i++)
        {
            var die = deck[i];
            if (die == null) continue;

            if (dieButtonPrefab == null)
            {
                Debug.LogError("FacePickerView: dieButtonPrefab is missing.", this);
                return;
            }

            var go = Instantiate(dieButtonPrefab, diceLayoutContainer);
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = die.dieName;

            var view = go.GetComponent<DiceTrayButtonView>();
            if (view != null)
            {
                view.SetIcon(die.uiIcon);
                view.SetSelected(false);
                view.SetSelectedIconShakeEnabled(false);
                _diceViews[die] = view;
            }
            else
            {
                Debug.LogError("FacePickerView: dieButtonPrefab needs DiceTrayButtonView.", go);
            }

            var btn = go.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogError("FacePickerView: dieButtonPrefab needs Button.", go);
                continue;
            }
            _diceButtons[die] = btn;
            var captured = die;
            btn.onClick.AddListener(() => OnDieClicked(captured));
            RegisterDieHover(btn, captured);
            SetDieButtonInteractable(captured, _selectedRewardFace != null && captured.CanAttachFace(_selectedRewardFace));
        }
    }

    private void ConfigureNavButtons()
    {
        if (backButton != null)
        {
            backButton.gameObject.SetActive(_onBack != null);
            backButton.onClick.RemoveAllListeners();
            if (_onBack != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(_onSkip != null);
            skipButton.onClick.RemoveAllListeners();
            if (_onSkip != null)
                skipButton.onClick.AddListener(OnSkipClicked);
        }
    }

    private void OnBackClicked()
    {
        if (_selectedRewardFace != null)
        {
            ReturnToFaceSelectionPhase();
            return;
        }

        Hide();
        _onBack?.Invoke();
    }

    private void OnSkipClicked()
    {
        Hide();
        _onSkip?.Invoke();
    }

    private void ReturnToFaceSelectionPhase()
    {
        if (_pickTransitionRoutine != null)
        {
            StopCoroutine(_pickTransitionRoutine);
            _pickTransitionRoutine = null;
        }

        _selectedRewardFace = null;
        _activeReplacementDie = null;
        _onRewindToFacePick?.Invoke();
        SetPhaseVisuals(phaseBActive: true);

        for (var i = 0; i < _rewardSlots.Count; i++)
        {
            var slot = _rewardSlots[i];
            if (slot == null) continue;
            slot.gameObject.SetActive(true);
            slot.SetInteractable(true);
            slot.SetHoverRevealEnabled(true);
            if (_rewardSlotGroups.TryGetValue(slot, out var cg) && cg != null)
                cg.alpha = 1f;
        }

        if (diceLayoutContainer != null)
        {
            var layout = diceLayoutContainer.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (layout != null)
                layout.spacing = 0f;
        }

        RebuildDiceLayout();
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        ClearDiceSelectionVisuals();
    }

    private void OnRewardFaceClicked(DieFaceSO face)
    {
        if (face == null || _selectedRewardFace != null) return;

        for (var i = 0; i < _rewardSlots.Count; i++)
            _rewardSlots[i]?.SetHoverRevealEnabled(false);

        _selectedRewardFace = face;
        SetPhaseVisuals(phaseBActive: false);
        _onFacePicked?.Invoke(face);

        if (_pickTransitionRoutine != null)
            StopCoroutine(_pickTransitionRoutine);
        _pickTransitionRoutine = StartCoroutine(CoRewardPickedTransition(face));

        foreach (var kv in _diceButtons)
            SetDieButtonInteractable(kv.Key, kv.Key.CanAttachFace(face));
    }

    private IEnumerator CoRewardPickedTransition(DieFaceSO selectedFace)
    {
        var layout = diceLayoutContainer != null ? diceLayoutContainer.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
        var startSpacing = layout != null ? layout.spacing : 0f;
        var endSpacing = diceLayoutSpacing;
        var duration = Mathf.Max(0.01f, pickTransitionSeconds);
        var t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            var u = Mathf.Clamp01(t / duration);
            var e = pickTransitionCurve != null ? pickTransitionCurve.Evaluate(u) : u;

            for (var i = 0; i < _rewardSlots.Count; i++)
            {
                var slot = _rewardSlots[i];
                if (slot == null || !_rewardSlotGroups.TryGetValue(slot, out var cg) || cg == null) continue;
                var keep = slot.Face == selectedFace;
                if (keep)
                {
                    cg.alpha = 1f;
                    continue;
                }

                cg.alpha = 1f - e;
            }

            if (layout != null)
                layout.spacing = Mathf.Lerp(startSpacing, endSpacing, e);
            yield return null;
        }

        for (var i = 0; i < _rewardSlots.Count; i++)
        {
            var slot = _rewardSlots[i];
            if (slot == null) continue;
            var keep = slot.Face == selectedFace;
            slot.gameObject.SetActive(keep);
        }

        if (layout != null)
            layout.spacing = endSpacing;
        _pickTransitionRoutine = null;
        ShowReplacementPanelForFirstCompatibleDie();
    }

    private void ShowReplacementPanelForFirstCompatibleDie()
    {
        if (dieTooltipOverlay == null || _selectedRewardFace == null) return;

        foreach (var kv in _diceButtons)
        {
            if (!kv.Key.CanAttachFace(_selectedRewardFace)) continue;
            OnDieClicked(kv.Key);
            return;
        }
    }

    private void OnDieClicked(DieAssetSO die)
    {
        if (_selectedRewardFace == null || die == null || !die.CanAttachFace(_selectedRewardFace))
            return;

        _activeReplacementDie = die;
        ClearDiceSelectionVisuals();
        if (_diceViews.TryGetValue(die, out var selectedView))
            selectedView.SetSelected(true);
        ShowDieReplacementPanel(die);
    }

    private void RegisterDieHover(Button btn, DieAssetSO die)
    {
        if (btn == null || die == null) return;
        var go = btn.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            if (_selectedRewardFace != null) return;
            if (dieTooltipOverlay == null) return;
            dieTooltipOverlay.ShowDie(die, false, null, GetDieIconRectForTooltip(die));
        });
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            if (_selectedRewardFace != null) return;
            dieTooltipOverlay?.Hide();
        });
        et.triggers.Add(exit);
    }

    private void ShowDieReplacementPanel(DieAssetSO die)
    {
        if (dieTooltipOverlay == null || die == null || _selectedRewardFace == null) return;
        if (!die.CanAttachFace(_selectedRewardFace)) return;
        dieTooltipOverlay.ShowDie(die, true, OnDieFaceReplacementClicked, GetDieIconRectForTooltip(die));
    }

    private RectTransform GetDieIconRectForTooltip(DieAssetSO die)
    {
        if (die == null) return null;
        return _diceViews.TryGetValue(die, out var view) ? view.IconRectTransform : null;
    }

    private void OnDieFaceReplacementClicked(int slotIndex, DieFaceSO oldFace)
    {
        if (_selectedRewardFace == null) return;
        var die = _activeReplacementDie;
        if (die == null) die = dieTooltipOverlay != null ? dieTooltipOverlay.CurrentDie : null;
        if (die == null) return;
        _onReplaceFaceSlotPicked?.Invoke(die, slotIndex);
    }

    private void SetDieButtonInteractable(DieAssetSO die, bool interactable)
    {
        if (die == null) return;
        if (_diceButtons.TryGetValue(die, out var button) && button != null)
            button.interactable = interactable;
        if (_diceViews.TryGetValue(die, out var view) && view != null)
            view.SetSelected(false);
    }

    private void ClearDiceSelectionVisuals()
    {
        foreach (var kv in _diceViews)
            kv.Value.SetSelected(false);
    }

    private void SetPhaseVisuals(bool phaseBActive)
    {
        SetObjectListActive(phaseBObjects, phaseBActive);
        SetObjectListActive(phaseCObjects, !phaseBActive);
    }

    private void SetAllPhaseObjects(bool active)
    {
        SetObjectListActive(phaseBObjects, active);
        SetObjectListActive(phaseCObjects, active);
    }

    private static void SetObjectListActive(List<GameObject> list, bool active)
    {
        if (list == null) return;
        for (var i = 0; i < list.Count; i++)
        {
            var go = list[i];
            if (go != null)
                go.SetActive(active);
        }
    }
}
