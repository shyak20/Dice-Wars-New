using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Spawns one UI die preview prefab per die in the selected character's starting deck
/// under a layout group (e.g. Horizontal Layout Group on <see cref="slotContainer"/>).
/// Prefab should include <see cref="DiceTrayButtonView"/> (recommended) or an <see cref="Image"/> for the die icon.
/// </summary>
public sealed class DiceSelectStartingDiceLayout : MonoBehaviour
{
    [Tooltip("Parent with a Layout Group (Horizontal or Grid) that arranges spawned die previews.")]
    [SerializeField] private Transform slotContainer;
    [FormerlySerializedAs("dieSlotPrefab")]
    [SerializeField] private GameObject dieSlotUiPrefab;
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;

    private readonly List<GameObject> _activeSlotObjects = new List<GameObject>();

    public void RefreshDeck(IReadOnlyList<DieAssetSO> deck)
    {
        ClearSlots();

        if (deck == null || deck.Count == 0)
            return;

        if (slotContainer == null)
        {
            Debug.LogError("DiceSelectStartingDiceLayout: assign slotContainer with a Layout Group.", this);
            return;
        }

        if (dieSlotUiPrefab == null)
        {
            Debug.LogError("DiceSelectStartingDiceLayout: assign dieSlotUiPrefab.", this);
            return;
        }

        if (slotContainer.GetComponent<LayoutGroup>() == null)
        {
            Debug.LogWarning(
                "DiceSelectStartingDiceLayout: slotContainer has no Layout Group; dice may overlap.",
                slotContainer);
        }

        for (var i = 0; i < deck.Count; i++)
        {
            var die = deck[i];
            if (die == null)
                continue;

            var go = Instantiate(dieSlotUiPrefab, slotContainer);
            SetupDieSlot(go, die);
            _activeSlotObjects.Add(go);
        }
    }

    void SetupDieSlot(GameObject slotObject, DieAssetSO die)
    {
        var trayView = slotObject.GetComponent<DiceTrayButtonView>();
        if (trayView != null)
        {
            trayView.SetIcon(die.uiIcon);
            trayView.SetSelected(false);
            trayView.SetSelectedIconShakeEnabled(false);
        }
        else
        {
            var iconImage = slotObject.GetComponentInChildren<Image>(true);
            if (iconImage != null)
            {
                iconImage.sprite = die.uiIcon;
                iconImage.enabled = die.uiIcon != null;
            }
            else
            {
                Debug.LogWarning(
                    $"DiceSelectStartingDiceLayout: prefab '{dieSlotUiPrefab.name}' has no DiceTrayButtonView or Image for '{die.dieName}'.",
                    slotObject);
            }
        }

        var nameLabel = slotObject.GetComponentInChildren<TMP_Text>(true);
        if (nameLabel != null)
            nameLabel.text = die.dieName;

        RegisterDieTooltipHover(slotObject, die, trayView);
    }

    void RegisterDieTooltipHover(GameObject slotObject, DieAssetSO die, DiceTrayButtonView trayView)
    {
        if (dieTooltipOverlay == null || slotObject == null || die == null)
            return;

        var et = slotObject.GetComponent<EventTrigger>() ?? slotObject.AddComponent<EventTrigger>();
        et.triggers.RemoveAll(entry =>
            entry.eventID == EventTriggerType.PointerEnter || entry.eventID == EventTriggerType.PointerExit);

        var iconRect = trayView != null ? trayView.IconRectTransform : slotObject.transform as RectTransform;

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => dieTooltipOverlay.ShowDie(die, false, null, iconRect));
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => dieTooltipOverlay.Hide());
        et.triggers.Add(exit);
    }

    void ClearSlots()
    {
        dieTooltipOverlay?.Hide();

        for (var i = 0; i < _activeSlotObjects.Count; i++)
        {
            if (_activeSlotObjects[i] != null)
                Destroy(_activeSlotObjects[i]);
        }

        _activeSlotObjects.Clear();
    }
}
