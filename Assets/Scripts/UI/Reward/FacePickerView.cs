using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Single-screen face reward flow:
/// 1) Pick one reward face.
/// 2) Rewards collapse to the picked face.
/// 3) Replacement uses in-tray <see cref="DieFaceSpreadView"/> (not the die tooltip).
/// </summary>
public class FacePickerView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject rewardSlotPrefab;

    [Header("Deck dice layout")]
    [SerializeField] private DieTrayFaceReplaceLayout trayLayout;

    [Header("Shared die tooltip")]
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;

    [Header("Phase Objects")]
    [Tooltip("Active only in phase B: face reward selection (before choosing a reward face).")]
    [SerializeField] private List<GameObject> phaseBObjects = new List<GameObject>();
    [Tooltip("Active only in phase C: face replacement (after choosing a reward face).")]
    [SerializeField] private List<GameObject> phaseCObjects = new List<GameObject>();

    [Header("Win-stage flow (optional)")]
    [SerializeField] private Button backButton;

    private readonly List<UIRewardSlot> _rewardSlots = new List<UIRewardSlot>();

    private Action<DieFaceSO> _onFacePicked;
    private Action<DieAssetSO, int, UIRewardSlot> _onReplaceFaceSlotPicked;
    private Action _onBack;
    private Action _onRewindToFacePick;
    private DieFaceSO _selectedRewardFace;
    private DieAssetSO _activeReplacementDie;
    private Coroutine _openSpreadRoutine;

    private void Awake()
    {
        if (panel == null) Debug.LogError("FacePickerView: assign panel.");
        if (slotContainer == null) Debug.LogError("FacePickerView: assign slotContainer.");
        if (rewardSlotPrefab == null) Debug.LogError("FacePickerView: assign rewardSlotPrefab (UIRewardSlot).");
        if (trayLayout == null) Debug.LogError("FacePickerView: assign trayLayout (DieTrayFaceReplaceLayout).");
    }

    private void Update()
    {
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
            if (trayLayout != null && trayLayout.DiceContainer != null && t.IsChildOf(trayLayout.DiceContainer)) return;
        }

        _activeReplacementDie = null;
        trayLayout?.ClearPinnedDieTooltip();
        dieTooltipOverlay.Hide();
    }

    public void Show(
        List<DieFaceSO> options,
        Action<DieFaceSO> onFacePicked,
        Action<DieAssetSO, int, UIRewardSlot> onReplaceFaceSlotPicked,
        Action onBack = null,
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
        _onRewindToFacePick = onRewindToFacePick;
        _selectedRewardFace = null;
        _activeReplacementDie = null;

        ConfigureNavButtons();
        RebuildRewardSlots(options);
        trayLayout.SetHoverTooltipsEnabled(true);
        trayLayout.CollapseAllFaceReplaceImmediate();
        RebuildDiceLayout();
        trayLayout.StartPrewarmSpreadsForCurrentEntries();
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        SetPhaseVisuals(phaseBActive: true);

        panel.SetActive(true);
    }

    public void Hide()
    {
        if (_openSpreadRoutine != null)
        {
            StopCoroutine(_openSpreadRoutine);
            _openSpreadRoutine = null;
        }

        trayLayout?.StopPrewarmSpreads();
        if (panel != null) panel.SetActive(false);
        trayLayout?.CollapseAllFaceReplaceImmediate();
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        SetAllPhaseObjects(false);
    }

    private void RebuildRewardSlots(List<DieFaceSO> options)
    {
        foreach (Transform c in slotContainer)
            Destroy(c.gameObject);
        _rewardSlots.Clear();

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
            slot.SetInteractable(true);
            slot.EnsureStandaloneHoverReveal();
            _rewardSlots.Add(slot);
        }
    }

    private void RebuildDiceLayout()
    {
        if (trayLayout == null || PlayerDataContainer.Instance?.RuntimeData == null)
            return;

        var deck = PlayerDataContainer.Instance.RuntimeData.currentDeck;
        Func<DieAssetSO, bool> faceReplaceFilter = _selectedRewardFace != null
            ? die => DieCanReceiveRewardFace(die, _selectedRewardFace)
            : null;

        trayLayout.Rebuild(
            deck,
            includeFilter: faceReplaceFilter,
            interactableFilter: faceReplaceFilter ?? (_ => true),
            onDieClicked: OnDieClicked);
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

    private void ReturnToFaceSelectionPhase()
    {
        _selectedRewardFace = null;
        _activeReplacementDie = null;
        _onRewindToFacePick?.Invoke();

        trayLayout.CollapseAllFaceReplace(() =>
        {
            SetPhaseVisuals(phaseBActive: true);

            for (var i = 0; i < _rewardSlots.Count; i++)
            {
                var slot = _rewardSlots[i];
                if (slot == null) continue;
                slot.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.SetHoverRevealEnabled(true);
            }

            trayLayout.SetHoverTooltipsEnabled(true);
            RebuildDiceLayout();
            trayLayout.StartPrewarmSpreadsForCurrentEntries();
            if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        });
    }

    private void OnRewardFaceClicked(DieFaceSO face)
    {
        if (face == null || _selectedRewardFace != null) return;

        for (var i = 0; i < _rewardSlots.Count; i++)
            _rewardSlots[i]?.SetHoverRevealEnabled(false);

        _selectedRewardFace = face;
        SetPhaseVisuals(phaseBActive: false);
        trayLayout.SetHoverTooltipsEnabled(false);
        trayLayout.ClearPinnedDieTooltip();
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        _onFacePicked?.Invoke(face);

        CollapseRewardSlotsToSelected(face);
        RebuildDiceLayout();
        trayLayout.StartPrewarmSpreadsForCurrentEntries();
        ShowReplacementSpreadForFirstCompatibleDie();
    }

    private void CollapseRewardSlotsToSelected(DieFaceSO selectedFace)
    {
        for (var i = 0; i < _rewardSlots.Count; i++)
        {
            var slot = _rewardSlots[i];
            if (slot == null) continue;
            slot.gameObject.SetActive(slot.Face == selectedFace);
        }
    }

    private void ShowReplacementSpreadForFirstCompatibleDie()
    {
        if (_selectedRewardFace == null || trayLayout == null || PlayerDataContainer.Instance?.RuntimeData == null)
            return;

        if (_openSpreadRoutine != null)
        {
            StopCoroutine(_openSpreadRoutine);
            _openSpreadRoutine = null;
        }

        _openSpreadRoutine = StartCoroutine(CoOpenFirstCompatibleSpread());
    }

    private IEnumerator CoOpenFirstCompatibleSpread()
    {
        var deck = PlayerDataContainer.Instance.RuntimeData.currentDeck;
        DieAssetSO targetDie = null;
        for (var i = 0; i < deck.Count; i++)
        {
            var die = deck[i];
            if (DieCanReceiveRewardFace(die, _selectedRewardFace))
            {
                targetDie = die;
                break;
            }
        }

        if (targetDie == null)
        {
            _openSpreadRoutine = null;
            yield break;
        }

        if (!trayLayout.IsSpreadPrewarmed(targetDie))
            yield return null;

        OnDieClicked(targetDie);
        _openSpreadRoutine = null;
    }

    private void OnDieClicked(DieAssetSO die)
    {
        if (die == null)
            return;

        if (_selectedRewardFace == null)
        {
            trayLayout.PinDieTooltip(die);
            return;
        }

        if (!DieCanReceiveSelectedRewardFace(die))
            return;

        _activeReplacementDie = die;
        trayLayout.SelectDieForFaceReplace(
            die,
            _selectedRewardFace,
            idx => SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, idx, _selectedRewardFace),
            OnDieFaceReplacementClicked);
    }

    bool DieCanReceiveSelectedRewardFace(DieAssetSO die) =>
        DieCanReceiveRewardFace(die, _selectedRewardFace);

    static bool DieCanReceiveRewardFace(DieAssetSO die, DieFaceSO face) =>
        PlayerInventory.IsDieEligibleForFaceReplacement(die, face);

    public void NotifyFaceReplacementRuleError() => trayLayout?.NotifyFaceReplacementRuleError();

    private void OnDieFaceReplacementClicked(int slotIndex, DieFaceSO oldFace, UIRewardSlot slot)
    {
        if (_selectedRewardFace == null) return;
        var die = _activeReplacementDie;
        if (die == null) return;
        _onReplaceFaceSlotPicked?.Invoke(die, slotIndex, slot);
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
