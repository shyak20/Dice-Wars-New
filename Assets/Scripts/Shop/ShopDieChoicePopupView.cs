using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopDieChoicePopupView : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] Button backButton;
    [SerializeField] DieTrayFaceReplaceLayout trayLayout;
    [SerializeField] DieTooltipOverlayUI dieTooltipOverlay;
    [Tooltip("After buying a face in the shop, delay before closing this popup (new-face preview on the clicked slot).")]
    [SerializeField, Min(0f)] private float faceSwapCloseDelay = 2f;

    DieFaceSO _targetFace;
    GemSO _targetGem;
    Func<DieAssetSO, int, bool> _onFaceCommit;
    Func<DieAssetSO, bool> _onGemCommit;
    Action _onCancel;
    Action _onFaceSwapClosed;
    DieAssetSO _activeDie;
    Coroutine _faceSwapCloseRoutine;
    Coroutine _openSpreadRoutine;

    void Awake()
    {
        if (trayLayout == null)
            trayLayout = GetComponentInChildren<DieTrayFaceReplaceLayout>(true);

        if (trayLayout == null)
            Debug.LogError($"ShopDieChoicePopupView on '{name}': assign trayLayout (DieTrayFaceReplaceLayout on the dice tray).");
    }

    public void ShowForFaceReplacement(DieFaceSO targetFace, Func<DieAssetSO, int, bool> onCommit, Action onCancel, Action onFaceSwapClosed = null)
    {
        _targetFace = targetFace;
        _targetGem = null;
        _onFaceCommit = onCommit;
        _onGemCommit = null;
        _onCancel = onCancel;
        _onFaceSwapClosed = onFaceSwapClosed;
        if (titleText != null) titleText.text = "Choose die and face to replace";
        ShowCommon(faceReplaceMode: true);
    }

    public void ShowForGemSocket(GemSO targetGem, Func<DieAssetSO, bool> onCommit, Action onCancel)
    {
        _targetFace = null;
        _targetGem = targetGem;
        _onFaceCommit = null;
        _onGemCommit = onCommit;
        _onCancel = onCancel;
        _onFaceSwapClosed = null;
        if (titleText != null) titleText.text = "Choose die for gem";
        ShowCommon(faceReplaceMode: false);
    }

    void ShowCommon(bool faceReplaceMode)
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Cancel);
        }

        if (trayLayout == null)
        {
            Debug.LogError($"ShopDieChoicePopupView on '{name}': cannot show — trayLayout is not assigned.");
            return;
        }

        trayLayout.SetHoverTooltipsEnabled(!faceReplaceMode);
        trayLayout.CollapseAllFaceReplaceImmediate();
        RebuildDice();
        if (faceReplaceMode)
        {
            trayLayout.StartPrewarmSpreadsForCurrentEntries();
            OpenFirstCompatibleDieSpread();
        }
        _activeDie = null;
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        if (panel != null) panel.SetActive(true);
    }

    void RebuildDice()
    {
        if (trayLayout == null || PlayerDataContainer.Instance?.RuntimeData == null) return;

        var deck = PlayerDataContainer.Instance.RuntimeData.currentDeck;
        trayLayout.Rebuild(
            deck,
            includeFilter: die =>
            {
                if (die == null) return false;
                if (_targetFace != null)
                    return die.CanAttachFace(_targetFace)
                           && SameValueFaceCapUtility.DieHasAnyLegalReplacementSlot(die, _targetFace);
                if (_targetGem != null)
                    return die.GetEmptyGemSocketCount() > 0;
                return false;
            },
            onDieClicked: OnDieClicked);
    }

    void OpenFirstCompatibleDieSpread()
    {
        if (_targetFace == null || trayLayout == null)
            return;

        if (_openSpreadRoutine != null)
        {
            StopCoroutine(_openSpreadRoutine);
            _openSpreadRoutine = null;
        }

        _openSpreadRoutine = StartCoroutine(CoOpenFirstCompatibleSpread());
    }

    IEnumerator CoOpenFirstCompatibleSpread()
    {
        if (_targetFace == null || trayLayout == null || PlayerDataContainer.Instance?.RuntimeData == null)
            yield break;

        var deck = PlayerDataContainer.Instance.RuntimeData.currentDeck;
        DieAssetSO targetDie = null;
        for (var i = 0; i < deck.Count; i++)
        {
            var die = deck[i];
            if (die == null)
                continue;
            if (die.CanAttachFace(_targetFace)
                && SameValueFaceCapUtility.DieHasAnyLegalReplacementSlot(die, _targetFace))
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

    void OnDieClicked(DieAssetSO die)
    {
        _activeDie = die;

        if (_targetFace != null)
        {
            trayLayout.SelectDieForFaceReplace(
                die,
                _targetFace,
                idx => SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, idx, _targetFace),
                (slotIndex, _, slot) =>
                {
                    if (_onFaceCommit == null || !_onFaceCommit(die, slotIndex))
                        return;

                    if (_faceSwapCloseRoutine != null)
                        StopCoroutine(_faceSwapCloseRoutine);

                    _faceSwapCloseRoutine = StartCoroutine(CoCloseAfterFaceSwapPreview(slot, _targetFace));
                });
            return;
        }

        if (_targetGem != null)
        {
            trayLayout.SetSelectedDie(die);
            dieTooltipOverlay?.ShowDie(die, false, null, trayLayout.GetDieIconRect(die));
            if (_onGemCommit != null && _onGemCommit(die))
                Hide();
        }
    }

    IEnumerator CoCloseAfterFaceSwapPreview(UIRewardSlot slot, DieFaceSO newFace)
    {
        if (backButton != null)
            backButton.interactable = false;

        trayLayout.SetAllSpreadSlotsInteractable(false);

        if (slot != null && newFace != null)
            slot.ShowNewFacePickedPreview(newFace);

        if (faceSwapCloseDelay > 0f)
            yield return new WaitForSeconds(faceSwapCloseDelay);

        var onClosed = _onFaceSwapClosed;
        _onFaceSwapClosed = null;
        _faceSwapCloseRoutine = null;

        Hide();

        if (backButton != null)
            backButton.interactable = true;

        onClosed?.Invoke();
    }

    public void NotifyFaceReplacementRuleError() => trayLayout?.NotifyFaceReplacementRuleError();

    void Cancel()
    {
        Hide();
        _onCancel?.Invoke();
    }

    public void Hide()
    {
        if (_openSpreadRoutine != null)
        {
            StopCoroutine(_openSpreadRoutine);
            _openSpreadRoutine = null;
        }

        if (_faceSwapCloseRoutine != null)
        {
            StopCoroutine(_faceSwapCloseRoutine);
            _faceSwapCloseRoutine = null;
        }

        _onFaceSwapClosed = null;
        trayLayout?.StopPrewarmSpreads();
        if (panel != null) panel.SetActive(false);
        trayLayout?.CollapseAllFaceReplaceImmediate();
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        _activeDie = null;

        if (backButton != null)
            backButton.interactable = true;
    }
}
