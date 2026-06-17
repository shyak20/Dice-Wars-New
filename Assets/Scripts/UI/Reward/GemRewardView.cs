using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Win-stage gem reward screen: choose which die receives the gem.
/// Shows die tooltip on hover, same as FacePickerView.
/// </summary>
public class GemRewardView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text gemTitleText;
    [SerializeField] private Image gemIconImage;
    [SerializeField] private TMP_Text gemDescriptionText;
    [SerializeField] private Transform diceLayoutContainer;
    [SerializeField] private GameObject dieButtonPrefab;
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;
    [SerializeField] private Button backButton;

    private readonly Dictionary<DieAssetSO, DiceTrayButtonView> _diceViews = new Dictionary<DieAssetSO, DiceTrayButtonView>();
    private readonly Dictionary<DieAssetSO, Button> _diceButtons = new Dictionary<DieAssetSO, Button>();
    private GemSO _gem;
    private Action<DieAssetSO> _onDieChosen;
    private Action _onBack;

    private void Awake()
    {
        if (panel == null) Debug.LogError("GemRewardView: assign panel.");
        if (diceLayoutContainer == null) Debug.LogError("GemRewardView: assign diceLayoutContainer.");
        if (dieButtonPrefab == null) Debug.LogError("GemRewardView: assign dieButtonPrefab.");
    }

    public void Show(GemSO gem, Action<DieAssetSO> onDieChosen, Action onBack = null)
    {
        _gem = gem;
        _onDieChosen = onDieChosen;
        _onBack = onBack;

        if (gemTitleText != null) gemTitleText.text = gem != null ? gem.DisplayLabel : "Gem";
        if (gemDescriptionText != null) gemDescriptionText.text = gem != null ? gem.description : string.Empty;
        if (gemIconImage != null)
        {
            var icon = gem != null ? gem.icon : null;
            gemIconImage.sprite = icon;
            gemIconImage.enabled = icon != null;
        }

        if (backButton != null)
        {
            backButton.gameObject.SetActive(_onBack != null);
            backButton.onClick.RemoveAllListeners();
            if (_onBack != null) backButton.onClick.AddListener(OnBackClicked);
        }

        RebuildDiceLayout();
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        if (dieTooltipOverlay != null) dieTooltipOverlay.Hide();
    }

    private void RebuildDiceLayout()
    {
        if (diceLayoutContainer == null || PlayerDataContainer.Instance?.RuntimeData == null)
            return;

        foreach (Transform c in diceLayoutContainer)
            Destroy(c.gameObject);
        _diceViews.Clear();
        _diceButtons.Clear();

        var deck = PlayerInventory.GetDiceWithEmptyGemSocket(PlayerDataContainer.Instance.RuntimeData);
        for (var i = 0; i < deck.Count; i++)
        {
            var die = deck[i];
            if (die == null) continue;

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
                Debug.LogError("GemRewardView: dieButtonPrefab needs DiceTrayButtonView.", go);
            }

            var btn = go.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogError("GemRewardView: dieButtonPrefab needs Button.", go);
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
        if (btn == null || die == null) return;
        var go = btn.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            if (dieTooltipOverlay == null) return;
            dieTooltipOverlay.ShowDie(die, false, null, GetDieIconRectForTooltip(die));
        });
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => dieTooltipOverlay?.Hide());
        et.triggers.Add(exit);
    }

    private RectTransform GetDieIconRectForTooltip(DieAssetSO die)
    {
        if (die == null) return null;
        return _diceViews.TryGetValue(die, out var view) ? view.IconRectTransform : null;
    }

    private void OnDieClicked(DieAssetSO die)
    {
        if (die == null || _gem == null) return;
        if (!die.TrySocketGem(_gem))
        {
            Debug.LogWarning($"GemRewardView: failed to socket gem '{_gem.name}' into die '{die.dieName}'.");
            return;
        }

        Hide();
        _onDieChosen?.Invoke(die);
    }

    private void OnBackClicked()
    {
        Hide();
        _onBack?.Invoke();
    }
}
