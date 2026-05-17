using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Map unknown-event die picker: deck tray with <see cref="DieTooltipOverlayUI"/> hover and Back to return without committing.
/// </summary>
public sealed class MapUnknownEventDieChoicePopupView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button backButton;
    [SerializeField] private Transform diceLayoutContainer;
    [SerializeField] private GameObject dieButtonPrefab;
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;

    private readonly Dictionary<DieAssetSO, DiceTrayButtonView> _diceViews = new();
    private readonly Dictionary<DieAssetSO, Button> _diceButtons = new();
    private DieAssetSO _selectedDie;
    private Func<DieAssetSO, bool> _dieFilter;
    private Action<DieAssetSO> _onCommit;
    private Action _onCancel;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (panel == null)
            Debug.LogError("MapUnknownEventDieChoicePopupView: assign panel.", this);
        if (diceLayoutContainer == null)
            Debug.LogError("MapUnknownEventDieChoicePopupView: assign diceLayoutContainer.", this);
        if (dieButtonPrefab == null)
            Debug.LogError("MapUnknownEventDieChoicePopupView: assign dieButtonPrefab.", this);
    }
#endif

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void Show(string title, Func<DieAssetSO, bool> dieFilter, Action<DieAssetSO> onCommit, Action onCancel)
    {
        _dieFilter = dieFilter;
        _onCommit = onCommit;
        _onCancel = onCancel;
        _selectedDie = null;

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(title) ? "Choose a die" : title.Trim();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Cancel);
        }

        RebuildDiceLayout();
        if (dieTooltipOverlay != null)
            dieTooltipOverlay.Hide();
        if (panel != null)
            panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
        if (dieTooltipOverlay != null)
            dieTooltipOverlay.Hide();
        _selectedDie = null;
        _dieFilter = null;
        _onCommit = null;
        _onCancel = null;
    }

    private void RebuildDiceLayout()
    {
        if (diceLayoutContainer == null || PlayerDataContainer.Instance?.RuntimeData == null)
            return;

        foreach (Transform c in diceLayoutContainer)
            Destroy(c.gameObject);
        _diceViews.Clear();
        _diceButtons.Clear();

        var deck = PlayerDataContainer.Instance.RuntimeData.currentDeck;
        for (var i = 0; i < deck.Count; i++)
        {
            var die = deck[i];
            if (die == null)
                continue;
            if (_dieFilter != null && !_dieFilter(die))
                continue;

            var go = Instantiate(dieButtonPrefab, diceLayoutContainer);
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = die.dieName;

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
                Debug.LogError("MapUnknownEventDieChoicePopupView: dieButtonPrefab needs DiceTrayButtonView.", go);
            }

            var btn = go.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogError("MapUnknownEventDieChoicePopupView: dieButtonPrefab needs Button.", go);
                continue;
            }

            _diceButtons[die] = btn;
            var captured = die;
            btn.onClick.AddListener(() => OnDieClicked(captured));
            RegisterDieHover(btn, captured);
        }
    }

    private void RegisterDieHover(Button btn, DieAssetSO die)
    {
        if (btn == null || die == null)
            return;

        var go = btn.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            if (dieTooltipOverlay == null)
                return;
            dieTooltipOverlay.ShowDie(die, false, null, GetDieIconRect(die));
        });
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            if (_selectedDie == null)
                dieTooltipOverlay?.Hide();
        });
        et.triggers.Add(exit);
    }

    private RectTransform GetDieIconRect(DieAssetSO die) =>
        die != null && _diceViews.TryGetValue(die, out var view) ? view.IconRectTransform : null;

    private void OnDieClicked(DieAssetSO die)
    {
        if (die == null)
            return;

        _selectedDie = die;
        foreach (var kv in _diceViews)
            kv.Value.SetSelected(kv.Key == die);

        if (dieTooltipOverlay != null)
            dieTooltipOverlay.ShowDie(die, false, null, GetDieIconRect(die));

        var commit = _onCommit;
        Hide();
        commit?.Invoke(die);
    }

    private void Cancel()
    {
        var cancel = _onCancel;
        Hide();
        cancel?.Invoke();
    }
}
