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

    [Header("Skip for coins (optional)")]
    [SerializeField] private Button skipForCoinsButton;
    [SerializeField] private TMP_Text skipCoinsAmountText;
    [Tooltip("string.Format pattern shown when the picker opens and after skip; {0} = rolled coin amount.")]
    [SerializeField] private string skipCoinsAmountFormat = "+{0} Coins";

    private readonly List<UIRewardSlot> _rewardSlots = new List<UIRewardSlot>();

    private Action<DieFaceSO> _onFacePicked;
    private Action<DieAssetSO, int, UIRewardSlot> _onReplaceFaceSlotPicked;
    private Action _onBack;
    private Action _onRewindToFacePick;
    private Action _onSkipForCoins;
    private int _skipCoinsPreviewAmount;
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
        Action onRewindToFacePick = null,
        Action onSkipForCoins = null,
        int skipCoinsPreviewAmount = 0)
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
        _onSkipForCoins = onSkipForCoins;
        _skipCoinsPreviewAmount = skipCoinsPreviewAmount;
        _selectedRewardFace = null;
        _activeReplacementDie = null;

        panel.SetActive(true);
        SetPhaseVisuals(phaseBActive: true);
        EnsurePickerSubviewActive(trayLayout);
        EnsurePickerSubviewActive(skipCoinsAmountText);
        PrepareSkipCoinsTextForShow();
        ConfigureNavButtons();
        ConfigureSkipForCoinsButton();
        RebuildRewardSlots(options);
        if (trayLayout != null)
        {
            trayLayout.SetHoverTooltipsEnabled(true);
            trayLayout.CollapseAllFaceReplaceImmediate();
            RebuildDiceLayout();
            if (trayLayout.isActiveAndEnabled)
                trayLayout.StartPrewarmSpreadsForCurrentEntries();
        }

        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
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
        ClearSkipCoinsText();
    }

    static string FormatSkipCoinsAmount(int amount, string displayFormat)
    {
        if (string.IsNullOrEmpty(displayFormat))
            return $"+{amount}";

        try
        {
            return string.Format(displayFormat, amount);
        }
        catch (FormatException)
        {
            return amount.ToString();
        }
    }

    void PrepareSkipCoinsTextForShow()
    {
        if (skipCoinsAmountText == null)
            return;

        EnsurePickerSubviewActive(skipCoinsAmountText);
        if (_onSkipForCoins != null && _skipCoinsPreviewAmount > 0)
            skipCoinsAmountText.text = FormatSkipCoinsAmount(_skipCoinsPreviewAmount, skipCoinsAmountFormat);
        else
            skipCoinsAmountText.text = string.Empty;
    }

    void ClearSkipCoinsText()
    {
        if (skipCoinsAmountText != null)
            skipCoinsAmountText.text = string.Empty;
    }

    void EnsurePickerSubviewActive(Component target)
    {
        if (target == null)
            return;

        var stop = panel != null ? panel.transform : transform;
        EnsureAncestorsActive(target.transform, stop);
    }

    static void EnsureAncestorsActive(Transform leaf, Transform stopInclusive)
    {
        if (leaf == null)
            return;

        var stack = new List<Transform>();
        var t = leaf;
        while (t != null)
        {
            stack.Add(t);
            if (stopInclusive != null && t == stopInclusive)
                break;
            t = t.parent;
        }

        for (var i = stack.Count - 1; i >= 0; i--)
            stack[i].gameObject.SetActive(true);
    }

    void ConfigureSkipForCoinsButton()
    {
        if (skipForCoinsButton == null)
            return;

        var show = _onSkipForCoins != null && _selectedRewardFace == null;
        skipForCoinsButton.gameObject.SetActive(show);
        skipForCoinsButton.interactable = show;
        skipForCoinsButton.onClick.RemoveAllListeners();
        if (show)
            skipForCoinsButton.onClick.AddListener(OnSkipForCoinsClicked);
    }

    void OnSkipForCoinsClicked()
    {
        if (_selectedRewardFace != null || _onSkipForCoins == null)
            return;

        skipForCoinsButton.interactable = false;
        _onSkipForCoins.Invoke();
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

        PrepareSkipCoinsTextForShow();
        ConfigureSkipForCoinsButton();
        if (backButton != null)
            backButton.gameObject.SetActive(_onBack != null);

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
        if (skipForCoinsButton != null)
            skipForCoinsButton.gameObject.SetActive(false);
        ClearSkipCoinsText();
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
