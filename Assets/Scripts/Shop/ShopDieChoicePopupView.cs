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
    [SerializeField, Min(0f)] private float faceSwapCloseDelay = 1.25f;

    DieFaceSO _targetFace;
    GemSO _targetGem;
    Func<DieAssetSO, int, bool> _onFaceCommit;
    Func<DieAssetSO, bool> _onGemCommit;
    Action _onCancel;
    DieAssetSO _activeDie;

    public void ShowForFaceReplacement(DieFaceSO targetFace, Func<DieAssetSO, int, bool> onCommit, Action onCancel)
    {
        _targetFace = targetFace;
        _targetGem = null;
        _onFaceCommit = onCommit;
        _onGemCommit = null;
        _onCancel = onCancel;
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

        trayLayout.SetHoverTooltipsEnabled(!faceReplaceMode);
        trayLayout.CollapseAllFaceReplaceImmediate();
        RebuildDice();
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
                    StartCoroutine(CoCloseAfterFaceSwapPreview(slot, _targetFace));
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
        trayLayout.SetAllSpreadSlotsInteractable(false);

        if (slot != null && newFace != null)
            slot.ShowNewFacePickedPreview(newFace);

        if (faceSwapCloseDelay > 0f)
            yield return new WaitForSeconds(faceSwapCloseDelay);

        Hide();
    }

    public void NotifyFaceReplacementRuleError() => trayLayout?.NotifyFaceReplacementRuleError();

    void Cancel()
    {
        Hide();
        _onCancel?.Invoke();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        trayLayout?.CollapseAllFaceReplaceImmediate();
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        _activeDie = null;
    }
}
