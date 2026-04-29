using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopDieChoicePopupView : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] Button backButton;
    [SerializeField] Transform diceLayoutContainer;
    [SerializeField] GameObject dieButtonPrefab;
    [SerializeField] DieTooltipOverlayUI dieTooltipOverlay;

    readonly Dictionary<DieAssetSO, DiceTrayButtonView> _views = new();
    readonly Dictionary<DieAssetSO, Button> _buttons = new();
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
        ShowCommon();
    }

    public void ShowForGemSocket(GemSO targetGem, Func<DieAssetSO, bool> onCommit, Action onCancel)
    {
        _targetFace = null;
        _targetGem = targetGem;
        _onFaceCommit = null;
        _onGemCommit = onCommit;
        _onCancel = onCancel;
        if (titleText != null) titleText.text = "Choose die for gem";
        ShowCommon();
    }

    void ShowCommon()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Cancel);
        }

        RebuildDice();
        _activeDie = null;
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        if (panel != null) panel.SetActive(true);
    }

    void RebuildDice()
    {
        if (diceLayoutContainer == null || PlayerDataContainer.Instance?.RuntimeData == null) return;
        foreach (Transform c in diceLayoutContainer) Destroy(c.gameObject);
        _views.Clear();
        _buttons.Clear();

        var deck = PlayerDataContainer.Instance.RuntimeData.currentDeck;
        for (var i = 0; i < deck.Count; i++)
        {
            var die = deck[i];
            if (die == null) continue;

            var faceCompatible = _targetFace != null && die.CanAttachFace(_targetFace);
            var gemCompatible = _targetGem != null && die.GetEmptyGemSocketCount() > 0;
            var compatible = faceCompatible || gemCompatible;
            if (!compatible) continue;

            var go = Instantiate(dieButtonPrefab, diceLayoutContainer);
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = die.dieName;
            var view = go.GetComponent<DiceTrayButtonView>();
            if (view != null)
            {
                view.SetIcon(die.uiIcon);
                view.SetSelected(false);
                view.SetSelectedIconShakeEnabled(false);
                _views[die] = view;
            }

            var btn = go.GetComponent<Button>();
            if (btn == null) continue;
            _buttons[die] = btn;
            var captured = die;
            btn.onClick.AddListener(() => OnDieClicked(captured));
            RegisterHover(btn, captured);
        }
    }

    void RegisterHover(Button btn, DieAssetSO die)
    {
        if (btn == null || die == null) return;
        var et = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { if (_activeDie == null) dieTooltipOverlay?.ShowDie(die, false, null, GetIconRect(die)); });
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { if (_activeDie == null) dieTooltipOverlay?.Hide(); });
        et.triggers.Add(enter);
        et.triggers.Add(exit);
    }

    void OnDieClicked(DieAssetSO die)
    {
        _activeDie = die;
        foreach (var kv in _views) kv.Value.SetSelected(kv.Key == die);
        if (_targetFace != null)
        {
            dieTooltipOverlay?.ShowDie(die, true, (slotIndex, _) =>
            {
                if (_onFaceCommit != null && _onFaceCommit(die, slotIndex))
                    Hide();
            }, GetIconRect(die));
            return;
        }

        if (_targetGem != null)
        {
            dieTooltipOverlay?.ShowDie(die, false, null, GetIconRect(die));
            if (_onGemCommit != null && _onGemCommit(die))
                Hide();
        }
    }

    RectTransform GetIconRect(DieAssetSO die) => die != null && _views.TryGetValue(die, out var v) ? v.IconRectTransform : null;

    void Cancel()
    {
        Hide();
        _onCancel?.Invoke();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        _activeDie = null;
    }
}
