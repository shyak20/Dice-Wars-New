using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shows hover text via <see cref="HoverTooltipManager"/> with <see cref="UIMapMoveCounterUI"/> description formatted using live map overflow values.
/// </summary>
public sealed class UIMapMovesHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    RectTransform _anchorRect;
    private MapMovementManager _manager;
    private string _title;
    private string _descriptionFormat;
    private Vector2 _screenOffset;
    private bool _isAbove;

    public void Initialize(
        RectTransform anchor,
        MapMovementManager manager,
        string title,
        string descriptionFormat,
        Vector2 screenOffset,
        bool isAbove = false)
    {
        _anchorRect = anchor;
        _manager = manager;
        _title = title ?? string.Empty;
        _descriptionFormat = descriptionFormat ?? string.Empty;
        _screenOffset = screenOffset;
        _isAbove = isAbove;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_anchorRect == null)
            return;
        if (string.IsNullOrWhiteSpace(_title) && string.IsNullOrWhiteSpace(_descriptionFormat))
            return;

        var mgr = HoverTooltipManager.Instance;
        if (mgr == null || !mgr.HasValidPrefab)
            return;

        var nextDamage = _manager != null ? _manager.GetCorruptionDamageForNextStep() : 0;
        var increase = _manager != null ? _manager.OverflowDamageIncreasePerMove : 0;
        var description = string.Format(_descriptionFormat, nextDamage, increase);
        mgr.Show(_anchorRect, _screenOffset, _title, description, isAbove: _isAbove);
    }

    public void OnPointerExit(PointerEventData eventData) => HoverTooltipManager.HideAllTooltipPanels();

    private void OnDisable() => HoverTooltipManager.HideAllTooltipPanels();
}
