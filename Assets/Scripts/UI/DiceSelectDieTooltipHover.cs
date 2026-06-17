using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shows <see cref="DieTooltipOverlayUI"/> while the pointer hovers a dice-select deck preview button.
/// </summary>
[DisallowMultipleComponent]
public sealed class DiceSelectDieTooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    DieAssetSO _die;
    DieTooltipOverlayUI _overlay;

    public void Bind(DieAssetSO die, DieTooltipOverlayUI overlay)
    {
        _die = die;
        _overlay = overlay;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_die == null || _overlay == null)
            return;

        // Keep the tooltip at its authored scene position (no horizontal align to the die icon).
        _overlay.ShowDie(_die, false, null, null);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_overlay == null)
            return;

        // Moving between dice in the tray should not hide then immediately re-show (flicker).
        if (eventData.pointerEnter != null &&
            eventData.pointerEnter.GetComponentInParent<DiceSelectDieTooltipHover>() != null)
            return;

        _overlay.Hide();
    }
}
