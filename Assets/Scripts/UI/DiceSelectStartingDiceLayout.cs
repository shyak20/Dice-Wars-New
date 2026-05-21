using System.Collections.Generic;
using UnityEngine;
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
    [SerializeField] private GameObject dieSlotUiPrefab;
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;
    [Tooltip("Instantiated under the scene Canvas when no DieTooltipOverlayUI is found (e.g. Selected Die Tooltip prefab).")]
    [SerializeField] private GameObject dieTooltipOverlayPrefab;

    private readonly List<GameObject> _activeSlotObjects = new List<GameObject>();
    private DieTooltipOverlayUI _runtimeTooltipOverlay;

    void Awake()
    {
        EnsureTooltipOverlay();
    }

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

        if (!EnsureTooltipOverlay())
            return;

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

    bool EnsureTooltipOverlay()
    {
        if (_runtimeTooltipOverlay != null)
            return true;

        if (dieTooltipOverlay != null)
        {
            _runtimeTooltipOverlay = dieTooltipOverlay;
            ConfigureOverlayForDiceSelect();
            return true;
        }

        _runtimeTooltipOverlay = GetComponentInChildren<DieTooltipOverlayUI>(true);
        if (_runtimeTooltipOverlay != null)
        {
            ConfigureOverlayForDiceSelect();
            return true;
        }

        _runtimeTooltipOverlay = FindObjectOfType<DieTooltipOverlayUI>(true);
        if (_runtimeTooltipOverlay != null)
        {
            ConfigureOverlayForDiceSelect();
            return true;
        }

        if (dieTooltipOverlayPrefab != null)
        {
            var canvas = GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.transform : transform;
            var instance = Instantiate(dieTooltipOverlayPrefab, parent);
            _runtimeTooltipOverlay = instance.GetComponent<DieTooltipOverlayUI>();
            if (_runtimeTooltipOverlay == null)
            {
                Debug.LogError(
                    $"DiceSelectStartingDiceLayout: dieTooltipOverlayPrefab '{dieTooltipOverlayPrefab.name}' needs DieTooltipOverlayUI.",
                    dieTooltipOverlayPrefab);
                Destroy(instance);
                return false;
            }

            ConfigureOverlayForDiceSelect();
            return true;
        }

        Debug.LogError(
            "DiceSelectStartingDiceLayout: assign dieTooltipOverlay or dieTooltipOverlayPrefab (Selected Die Tooltip).",
            this);
        return false;
    }

    void ConfigureOverlayForDiceSelect()
    {
        _runtimeTooltipOverlay.SetDecorativeRaycastBlocking(false);
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
        if (_runtimeTooltipOverlay == null || slotObject == null || die == null)
            return;

        var hoverTarget = ResolveHoverTarget(slotObject);
        if (hoverTarget == null)
        {
            Debug.LogWarning($"DiceSelectStartingDiceLayout: no hover target on '{slotObject.name}'.", slotObject);
            return;
        }

        var hover = hoverTarget.GetComponent<DiceSelectDieTooltipHover>();
        if (hover == null)
            hover = hoverTarget.gameObject.AddComponent<DiceSelectDieTooltipHover>();

        hover.Bind(die, _runtimeTooltipOverlay);
    }

    static GameObject ResolveHoverTarget(GameObject slotObject)
    {
        var button = slotObject.GetComponent<Button>();
        if (button != null)
            return button.gameObject;

        button = slotObject.GetComponentInChildren<Button>(true);
        if (button != null)
            return button.gameObject;

        if (slotObject.GetComponent<Graphic>() != null)
            return slotObject;

        var graphic = slotObject.GetComponentInChildren<Graphic>(true);
        return graphic != null ? graphic.gameObject : null;
    }

    void ClearSlots()
    {
        _runtimeTooltipOverlay?.Hide();

        for (var i = 0; i < _activeSlotObjects.Count; i++)
        {
            if (_activeSlotObjects[i] != null)
                Destroy(_activeSlotObjects[i]);
        }

        _activeSlotObjects.Clear();
    }
}
