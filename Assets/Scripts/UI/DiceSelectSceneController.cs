using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dice pick screen: wires 3× <see cref="DiceSelectDieSlot"/>, title &quot;X/Y Starting dice&quot;, Continue → <see cref="RunManager.LoadMapAfterDiceSelect"/>.
/// </summary>
public sealed class DiceSelectSceneController : MonoBehaviour
{
    [SerializeField] private DiceSelectDieSlot[] dieSlots = new DiceSelectDieSlot[3];
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Button continueButton;
    [Tooltip("Optional blocker visual shown while Continue is locked.")]
    [SerializeField] private GameObject continueLockedOverlay;
    [SerializeField, Min(1)] private int requiredStartingDiceCount = 3;
    [SerializeField] private string titleFormat = "{0}/{1} Starting dice";

    /// <summary>Last die clicked — its tooltip stays visible until another die is hovered.</summary>
    private DiceSelectDieSlot _pinnedTooltipSlot;
    /// <summary>While non-null, overrides pinned tooltip (hover takes priority).</summary>
    private DiceSelectDieSlot _hoverTooltipSlot;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
    }

    private void Start()
    {
        if (dieSlots == null || dieSlots.Length == 0)
            dieSlots = GetComponentsInChildren<DiceSelectDieSlot>(true);

        if (dieSlots.Length < requiredStartingDiceCount)
            Debug.LogError($"DiceSelectSceneController: need at least {requiredStartingDiceCount} die slots (found {dieSlots.Length}).", this);

        foreach (var s in dieSlots)
        {
            if (s != null)
                s.Initialize(this);
        }

        RefreshSelectionUi();
        RefreshTooltips();
    }

    internal void NotifyTooltipHoverEnter(DiceSelectDieSlot slot)
    {
        if (slot == null)
            return;
        _hoverTooltipSlot = slot;
        RefreshTooltips();
    }

    internal void NotifyTooltipHoverExit(DiceSelectDieSlot slot)
    {
        if (_hoverTooltipSlot == slot)
            _hoverTooltipSlot = null;
        RefreshTooltips();
    }

    /// <summary>Call after the player clicks a die — that tooltip stays until hover moves to another die.</summary>
    internal void NotifyTooltipPinned(DiceSelectDieSlot slot)
    {
        _pinnedTooltipSlot = slot;
        RefreshTooltips();
    }

    internal void NotifyTooltipSlotDisabled(DiceSelectDieSlot slot)
    {
        if (_hoverTooltipSlot == slot)
            _hoverTooltipSlot = null;
        if (_pinnedTooltipSlot == slot)
            _pinnedTooltipSlot = null;
        RefreshTooltips();
    }

    private void RefreshTooltips()
    {
        var active = _hoverTooltipSlot != null ? _hoverTooltipSlot : _pinnedTooltipSlot;
        if (dieSlots == null)
            return;
        foreach (var s in dieSlots)
        {
            if (s == null)
                continue;
            var show = active != null && s == active;
            s.ApplyTooltipVisibility(show);
        }
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);
    }

    public void RefreshSelectionUi()
    {
        var count = 0;
        if (dieSlots != null)
        {
            foreach (var s in dieSlots)
            {
                if (s != null && s.IsSelected)
                    count++;
            }
        }

        if (titleLabel != null)
            titleLabel.text = string.Format(titleFormat, count, requiredStartingDiceCount);

        var canContinue = count == requiredStartingDiceCount;
        if (continueButton != null)
            continueButton.interactable = canContinue;
        if (continueLockedOverlay != null)
            continueLockedOverlay.SetActive(!canContinue);
    }

    private void OnContinueClicked()
    {
        var picked = new List<DieAssetSO>();
        if (dieSlots != null)
        {
            foreach (var s in dieSlots)
            {
                if (s == null || !s.IsSelected || s.StartingDie == null)
                    continue;
                picked.Add(s.StartingDie);
            }
        }

        if (picked.Count != requiredStartingDiceCount)
            return;

        if (PlayerDataContainer.Instance == null || PlayerDataContainer.Instance.RuntimeData == null)
        {
            Debug.LogError("DiceSelectSceneController: PlayerDataContainer missing — ensure it exists (e.g. DontDestroyOnLoad from Main Menu).", this);
            return;
        }

        PlayerDataContainer.Instance.ReplaceStartingDeck(picked);

        if (RunManager.Instance == null)
        {
            Debug.LogError("DiceSelectSceneController: RunManager missing.", this);
            return;
        }

        RunManager.Instance.LoadMapAfterDiceSelect();
    }
}
